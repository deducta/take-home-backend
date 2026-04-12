# Problems

## Some messages fail to enrich

The main problem from the run was that messages would sometimes transiently fail to be enriched and end up in the `document-submitted_error` queue. That seemed to be a result of repeated rate limiting errors and transient failures from MockAiApi. I found this by looking at the docker logs, the RabbitMQ console, and the code.

### Fixes

MockAiApi allows up to 20 requests per second, so for rate limiting I reduced ConcurrentMessageLimit to 20. (The previous value of 30 was causing some initial rate limiting errors, but the retry config prevented those from causing major correctness issues, though they likely caused throughput ones.)

For retries, I went with a simple increase in the number of retries from 2 to 5, and that seemed to be good enough, even in a case of 20,000 messages that I tried. In real cases I normally go with an interval or exponential retry mechanism or use information from the 429 response, but the only remaining errors are transient ones (since the concurrency limit ruled out rate limiting ones) and those just happen 10% of the time due to pure random chance so there's no advantage to progressively backing off.

You'd also want to have a way of re-processing messages from `document-submitted_error` so that those documents aren't lost.

## Enrichment is slow

This isn't a resilience problem, but the whole pipeline took a long time to process those messages. This was clearly just a problem in enrichment; ingestion and persistence were very fast. 

We could speed up enrichment by increasing the number of concurrent consumers and having them coordinate to not send more than 20 requests per second. MockAiApi's processing takes 2-5 seconds, so 5*20=100 consumers should get us to maximum throughput. 

Another option would be to horizontally scale MockAiApi, but that's beyond the scope of this exercise.

## Other potential problems

Code analysis also revealed a few potential problems, though none cropped up in my runs:

1. IngestionService:
    - No retries on RabbitMQ publish failures,
    - No dealing with partial batch failures in ingestion.
    - Can use MassTransit batch publish instead of individually publishing messages.
2. EnrichmentService:
    - Discussed above.
3. PersistenceService:
    - No uniqueness constraint on the DocumentId field, which could lead to duplicate document entries in the db. Dealing with this gracefully (rather than just accepting or ignoring unique constraint violation errors) would require doing an upsert rather than an insert.
    - ProcessingCount is hard-coded to 1. That seems wrong, but it's not clear what that field is for so it's not clear what it should be. (Should it count enrichment attempts, ie retries? Or is it for counting multiple completed enrichments that'd create duplicate document entries I just mentioned?)
    - No configuration of concurrency, leading to fallback to default ConcurrentMessageLimit values.

(There was also a lot of hard-coding of credentials and config in the code throughout that you wouldn't want in a real service but that's fine for an exercise like  this.)

Of these, the ones in the PersistenceService seem to be the most important, but given that they didn't cause issues in any of my runs, I didn't make any changes. To verify they didn't cause issues in the runs, I ran some db queries to check that there weren't any duplicate document ids and to sense-check the data in the db.

```sql
select count(*), count(distinct "DocumentId") from "DocumentResults"
-- select count(distinct "BatchId") from "DocumentResults"
-- select count("DocumentId") from "DocumentResults" group by "BatchId"
```

```sql
SELECT "Id",
       "DocumentId",
       "BatchId",
       "DocumentType",
       "Classification",
       "ExtractedEntities",
       "ConfidenceScore",
       "EnrichedAt",
       "PersistedAt",
       "ProcessingCount"
FROM public."DocumentResults"
ORDER BY "EnrichedAt" DESC
LIMIT 1000;
```

# Note

To be honest, I feel like I missed something (or some things) big. The solution seems good overall: clear separation of concerns, async pipelines, decent configuration of concurrency for MockAiApi's behaviour. I was able to create more problems by tweaking some of the config in EnrichmentService.cs (such as changing the concurrency config and removing the message retry config), but as it is the code seems mostly fine. If it wasn't for my unfamiliarity with C# and MassTransit, I would not have used most of the time.

A few other things:
- This is my first time doing something substantial in C#. (I played around with it for a few minutes years and years ago.) I spent a significant amount of time familiarising myself with the C# way of doing things, and especially with MassTransit.
- I used AI to ask a few questions about C# (though mostly I used Google) and to check my thinking at the end.
- I had trouble setting up dotnet outside of docker, so I ran the load generator using docker rather than native dotnet with `docker run --rm --network host -v "$PWD":/work -w /work/tests/LoadGenerator mcr.microsoft.com/dotnet/sdk:9.0 dotnet run`.
