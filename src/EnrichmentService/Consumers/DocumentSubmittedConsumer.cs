using System.Net.Http.Json;
using DocIntelligence.Contracts;
using MassTransit;

namespace EnrichmentService.Consumers;

public class DocumentSubmittedConsumer : IConsumer<Batch<DocumentSubmitted>>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DocumentSubmittedConsumer> _logger;
    private readonly IConfiguration _config;

    public DocumentSubmittedConsumer(
        HttpClient httpClient,
        ILogger<DocumentSubmittedConsumer> logger,
        IConfiguration config
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public async Task Consume(ConsumeContext<Batch<DocumentSubmitted>> context)
    {
        _logger.LogInformation("Processing documents in batch of {count}", context.Message.Length);

        var cancellationToken = context.CancellationToken;
        var enrichedDocuments = new List<DocumentEnriched>();
        var messageLimit = int.Parse(_config["MessageLimit"] ?? "10");

        await Parallel.ForEachAsync(
            context.Message,
            new ParallelOptions { MaxDegreeOfParallelism = messageLimit, CancellationToken = cancellationToken },
            async (consumedMessage, ct) =>
            {
                var message = consumedMessage.Message;
                var response = await _httpClient.PostAsJsonAsync(
                    "http://mockai:5050/api/enrich",
                    new { message.Content, message.DocumentType },
                    cancellationToken: ct
                );
                var result = response.Content.ReadFromJsonAsync<EnrichmentResult>(cancellationToken: ct).Result;
                enrichedDocuments.Add(new DocumentEnriched
                {
                    DocumentId = message.DocumentId,
                    BatchId = message.BatchId,
                    DocumentType = message.DocumentType,
                    Classification = result!.Classification,
                    ExtractedEntities = result.Entities.ToList(),
                    ConfidenceScore = result.Confidence,
                    EnrichedAt = DateTime.UtcNow
                });
            });

        await context.PublishBatch(enrichedDocuments, cancellationToken: cancellationToken);
    }
}