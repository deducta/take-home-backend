#### What problems you found (and how you found them)
By going through the codebase, it seems with high load the `MockAIApi` becomes bottleneck. It is mentioned to treat this service as external dependency.
Going through its codebase, we can see it has rate limit of 20rps, ~10% failure rate and 2-5 seconds processing time for each request.
Usually this information is available from the service providers, but also can be measured using monitoring tools at consumers side.

Knowing this, when 10K messages been passed to `IngestionService` -> `EnrichmentService`, they should be processed in batches to get optimal performance, in case
we are unable to scale `MockAIApi` or request higher rate limit.

As `MockAIApi` service is being only used by `EnrichmentService` as adapter or integrator, ideally this service should be able to handle processing large number of
messages for `MockAIApi`.

Going through the code of `EnrichmentService`, it can be seen that this service is calling `MockAIApi` APIs one at a time for each request.
In a high load scenario, e.g. 10K message this approach will take significant amount of time.
`EnrichmentService` can call `MockAIApi` API in batch of 20 requests and wait for ~5 seconds so all requests can finish processing before next batch. Or wait ~10 seconds to finish retry,
it will mostly need one retry, as from the logic in `MockAIApi` 10% of 20 is 2 and in next batch retry it should pass(though it works in simulation, real world scenario will be different).

`IngestionService` is sending messages to `EnrichmentService`. Which is also sending messages one at a time, and can use batch publishing
`PersistenceService` is receiving final result from `EnrichmentService` one at a time and saving them sequentially in DB, can also benefit with batching.

#### What you fixed and why you chose that approach
1. `EnrichmentService`:

---
Enabled batch message consumption in `Program.cs`
```c#
    x.AddConsumer<DocumentSubmittedConsumer>(cfg =>
    {
        cfg.Options<BatchOptions>(options =>
            options
                .SetMessageLimit(messageLimit)
                .SetTimeLimit(s: interval)
                .SetTimeLimitStart(BatchTimeLimitStart.FromLast)
        );
    });
```
As this service is calling `MockAIApi` in a batch of 20 messages, it also consumes 20 messages in batch, adn with a interval of 5 seconds as wait time to finish the batch process.

---
Added `MessageLimit` and `Interval` configs for consumer and batch process in configuration file `appsettings.json`.
```json
...
  "MessageLimit": 20,
  "Interval": 5
...
```
Now these settings can be changed without need to rebuild and redeploy the whole microservice. This follow `SOLID` principle of `open for extension but closed for modification`
If in future we can run bigger batch due to changes in ratelimit or can see improvement in response time, we can update those in infrastructure settings. Which is useful as it
reduces refactoring, retesting, redeploying and time consumption, which is often limited in startup and reduces chances of human errors.

---
Made `EnrichmentService` a `SingleActiveConsumer`.
```c#
x.AddConfigureEndpointsCallback((_, cfg) =>
{
    if (cfg is IRabbitMqReceiveEndpointConfigurator endpointConfigurator)
    endpointConfigurator.SingleActiveConsumer = true;
});
```
This settings prevents multiple connections for `EnrichmentService` in `RabbitMQ`, and confirms one consumer will consume the message.
As multiple connections will try to call `MockAIApi` in parallel and will have high failure due to rate limit.
From the code of `MockAIApi` it seems rate limit is based of any request. If in future this is improved and rate limit is done based on
API keys, tenants, or IPs `SingleActiveConsumer` will not be required.

---
Added `RateLimit` in receiver.
```c#
cfg.ReceiveEndpoint("document-submitted", e 
    ...
   e.UseRateLimit(messageLimit, TimeSpan.FromSeconds(interval));
```
As we are only processing 20 requests in a batch, adding ratelimit in receiver keeps the incoming messages in control. And a batch has time to finish all requests to `MockAIApi`
providing robustness and low failure due to ratelimit in `MockAIApi` service.

---
Updated `MessageRetry` to 5.
```c#
    e.UseMessageRetry(r => { r.Interval(5, TimeSpan.FromSeconds(1)); });
```
This will reduce failures in error_queue. as messages will have simple more retry option.

---
Used parallel batch calling to `MockAIApi` API
```c#
...
    public class DocumentSubmittedConsumer : IConsumer<Batch<DocumentSubmitted>>
...
    public async Task Consume(ConsumeContext<Batch<DocumentSubmitted>> context)
...
        await Parallel.ForEachAsync(
            context.Message,
            new ParallelOptions { MaxDegreeOfParallelism = messageLimit, CancellationToken = cancellationToken },
            async (consumedMessage, ct) =>
...
```
As we know `MockAIApi` has a rate limit of 20 rps and each request can take between 3-5 seconds to respond. Calling 20 requests at once will reduce
time consumption to ~3-5 seconds for 20 requests from 20*5=100 seconds of sequential calling. Which is significant improvement in processing time.

---
Publish messages to `PersistenceService` in batch
```c#
    await context.PublishBatch(enrichedDocuments, cancellationToken: cancellationToken);
```
> The PublishBatch method can be used to publish a batch of messages. This is useful for publishing a large number of messages. It’s nicer than calling Publish repeatedly in a loop awaiting each send operation, and uses Task.WhenAll to wait for all publish operations to complete.
https://masstransit.massient.com/concepts/producers#publish-a-batch-of-messages


2. `IngestionService`:

---
Removed for-each loop in favour of `PublishBatch`
```c#
    var documents =
                request.Documents.Select(doc => new DocumentSubmitted
...
    await publishEndpoint.PublishBatch(documents);
```
Ideally each microservice should be `loosely coupled` and have no or little knowledge of other services. It follows many of `SOLID` principle and cloud-native system design.
So, `IngestionService` should be able to publish messages in batch without knowledge of its consumer and what is the consumption capability it has.
Which can be controlled in consumer. In this way, consumer can be scaled or kept one and no changes will be necessary in publisher side.
Keeping code changes minimum to `IngestionService`.

3. `PersistenceService`

---
Kept `MessageLimit` configuration in `appsettings.json`
```json
...
  "MessageLimit": 10
...
```
For same reasons as in `IngestionService`

---
Added batch consumption option
```c#
 x.AddConsumer<DocumentEnrichedConsumer>(cfg =>
    {
        cfg.Options<BatchOptions>(options => options.SetMessageLimit(messageLimit));
    });
...
    cfg.PrefetchCount = messageLimit;
```
Though a `MessageLimit` is set, this can be updated based on the situation and without rebuilding the code.
In current situation this is dependent on `EnrichmentService` batch processing of 20 rps, but `IngestionService` don't necessarily needs any knowledge of that.
For `IngestionService` more important reason for the value of `MessageLimit` is batch insert on DB capacity.

---
Added batch saving of messages in DB
```c#
...
public class DocumentEnrichedConsumer : IConsumer<Batch<DocumentEnriched>>
...
        public async Task Consume(ConsumeContext<Batch<DocumentEnriched>> context)
...
        var results = context.Message.Select(consumedMessage =>
        {
            var message = consumedMessage.Message;
            return new DocumentResult
...
        _db.DocumentResults.AddRange(results);
...            
```
This reduces numbers of RTT to DB, keeping healthy and low number of DB calls.

#### What tradeoffs you considered

1. Retry process for `EnrichmentService` is basic, just increased the number to lower the chances. This could be much elegant.
2. Batch process retries entire batch for single or multiple request failure, will put the whole batch in error queue.
3.

#### What you would do differently with more time

1. Add tests
2. Add some observability, monitoring
3. Use better retry mechanism, e.g. Intervals, Incremental, Exponential etc. https://masstransit-v6.netlify.app/usage/exceptions.html#retry-configuration
4. Use conditional failure handling with MassTransit middleware, e.g. https://masstransit.massient.com/configuration/middleware/retry#configure-any-exception-filters
5. Handle error queue `document-submitted_error` to reprocess, e.g. https://www.rabbitmq.com/docs/shovel
6. Reduce hardcoding, secret handling
7. Handle infrastructure failure, e.g. publish (`IngestionService`, `EnrichmentService`), batch API (`IngestionService`), parallel calling of `MockAIApi` (`EnrichmentService`), DB save (`PersistenceService`) etc.