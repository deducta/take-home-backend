# Solution
## Summary
I focused on the two failures I could reproduce under load: requests exceeding the mock AI API's rate limit, and transient AI failures exhausting the existing retry policy.

## Problems Found and How I Investigated Them
1. **AI requests exceeded the downstream rate limit** 
I traced the message flow from ingestion through the DocumentSubmitted consumer, the mock AI HTTP endpoint, DocumentEnriched, and persistence. Load-test logs showed HTTP 429 Too Many Requests responses. 
Inspecting MockAiApi showed a limit of 20 requests within a rolling one-second window. The enrichment endpoint was configured with ConcurrentMessageLimit = 30 and PrefetchCount = 50, but neither setting limits the rate of outgoing HTTP requests. Fast failures and retries could contribute to bursts.

2. **Transient failures left documents in the error queue**
The mock API also deliberately returns HTTP 503 Service Unavailable for roughly 10% of otherwise eligible requests. The original MassTransit configuration allowed only two retries, one second apart. Under an initial 10,000-document test, 14 messages ended up in document-submitted_error queue; inspecting the messages in RabbitMQ UI showed 503 failures.
A few seconds of retrying helps with intermittent errors, but cannot guarantee success. Once the retry policy is exhausted, processing stops for that message unless it is recovered separately.

## Changes Made and Why
### A. Added an outbound AI rate limiter
I added AIRateLimiter as a singleton in the EnrichmentService and await it immediately before the HTTP request to the mock AI API.

The limiter combines:
- A rolling one-second window permitting at most 18 request starts.
- A 65 ms minimum interval between granted permits, to prevent bursts.
- A lock around timestamp checks and permit reservation, so concurrent consumers cannot acquire the same capacity.
- Cancellation-aware asynchronous waiting (currently polling every 100 ms), rather than blocking threads.
  
My first rolling-window-only implementation still produced some 429s. Adding minimum spacing addressed the burst behaviour observed in the logs. The subsequent load test showed no observed 429 responses.

I left `PrefetchCount = 50` and `ConcurrentMessageLimit = 30` unchanged to isolate the effect of outbound rate limiting. These settings bound concurrent message processing, not API request starts per second.

### B. Made HTTP retries selective and more resilient
I updated the MassTransit `UseMessageRetry` policy to use bounded exponential backoff: three retries, with a one-second minimum, ten-second maximum, and two-second interval delta.

The policy retries `HttpRequestException` for transient HTTP failures: `429`, `502`, `503` and `504`. It does not retry errors such as 400 Bad Request, because repeating an invalid request is unlikely to help.

This keeps retry decisions in the messaging pipeline instead of adding an HTTP retry loop inside the consumer. Failures that exhaust the policy still reach MassTransit's error queue for investigation.

### Observed Results

| Measurement |	Initial load test | After changes |
| -------- | ------- | ------- |
| Submitted documents | 10,000 | 10,000|
| Messages observed in document-submitted_error	| 14 |	3 |
| HTTP 429 responses | Observed | None observed |
| Final error-queue cause | 503	| All three were 503 |

These are results from separate runs against a mock API with random failures, so the error-count improvement is evidence of a better retry strategy, not a deterministic guarantee. All 9,997 record are accounted for within the postgres table.

## Trade-offs and Deliberate Scope
- **Reliability versus throughput**: 65 ms spacing caps the theoretical request-start rate at approximately 15.4/second, below the API's nominal limit of 20/second. This reduces overload but increases processing time.
- **Simple local coordination**: The singleton limiter coordinates concurrent consumers within one enrichment process. It would not enforce a shared limit across multiple replicas.
- **Polling and capacity**: The 100 ms permit polling interval is simple but can add latency and offers no fairness guarantee. MassTransit's in-memory retry delays also occupy consumer-processing capacity.
- **Bounded retries, not guaranteed delivery**: Three messages still exhausted their retries and entered the error queue. There is no automatic recovery of those messages in this change.
- **No broker upgrade**: I considered delayed redelivery but did not implement it. The current RabbitMQ image (rabbitmq:3-management) was retained to avoid introducing a broker/plugin change during a time-boxed reliability fix.

## What I Would Do With More Time
1. **Make persistence idempotent**: RabbitMQ can redeliver DocumentEnriched if the persistence service commits to PostgreSQL but crashes before acknowledging the message. Enforce uniqueness on DocumentId and use an idempotent insert or upsert so duplicate deliveries do not create duplicate records or unnecessary failures.
2. **Add durable recovery for transient failures**: Introduce delayed redelivery after short retries are exhausted, allowing documents to be retried later without occupying consumer slots during long delays. This would require a compatible RabbitMQ delayed-delivery mechanism. Also add alerting for error-queue growth and a safe process for replaying messages that exhaust all recovery attempts.
3. **Support multiple enrichment instances**: The current singleton AI rate limiter only coordinates requests within one process. For horizontal scaling, introduce a distributed rate limiter, potentially using an atomic Redis operation, so all instances share the downstream API's request budget. Make limits configurable and monitor throughput, permit wait times, HTTP errors and queue depth.