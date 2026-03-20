using System.Net.Http.Json;
using DocIntelligence.Contracts;
using MassTransit;

namespace EnrichmentService.Consumers;

public class DocumentSubmittedConsumer : IConsumer<DocumentSubmitted>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DocumentSubmittedConsumer> _logger;

    public DocumentSubmittedConsumer(HttpClient httpClient, ILogger<DocumentSubmittedConsumer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentSubmitted> context)
    {
        var message = context.Message;

        _logger.LogInformation("Processing document {DocumentId}", message.DocumentId);

        var response = await _httpClient.PostAsJsonAsync(
            "http://mockai:5050/api/enrich",
            new { message.Content, message.DocumentType });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EnrichmentResult>();

        await context.Publish(new DocumentEnriched
        {
            DocumentId = message.DocumentId,
            BatchId = message.BatchId,
            DocumentType = message.DocumentType,
            Classification = result!.Classification,
            ExtractedEntities = result.Entities.ToList(),
            ConfidenceScore = result.Confidence,
            EnrichedAt = DateTime.UtcNow
        });
    }
}
