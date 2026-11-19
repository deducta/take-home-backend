using System.Net.Http.Json;
using DocIntelligence.Contracts;
using EnrichmentService.Services;
using MassTransit;

namespace EnrichmentService.Consumers;

public class DocumentSubmittedConsumer : IConsumer<DocumentSubmitted>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DocumentSubmittedConsumer> _logger;
    private readonly AIRateLimiter _rateLimiter;
    private readonly int[] _retryableStatusCodes = [429, 503];

    public DocumentSubmittedConsumer(HttpClient httpClient, ILogger<DocumentSubmittedConsumer> logger, AIRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }
    
    public async Task Consume(ConsumeContext<DocumentSubmitted> context)
    {
        var message = context.Message;

        _logger.LogInformation("Processing document {DocumentId}", message.DocumentId);

        await _rateLimiter.WaitForPermitAsync(context.CancellationToken);

        var response = await _httpClient.PostAsJsonAsync(
            "http://mockai:5050/api/enrich",
            new { message.Content, message.DocumentType }, context.CancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AI enrichment failed for document {DocumentId}. Status={StatusCode}",
                message.DocumentId,
                (int)response.StatusCode);
        }
        
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
