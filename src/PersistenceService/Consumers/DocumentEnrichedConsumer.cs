using System.Text.Json;
using DocIntelligence.Contracts;
using MassTransit;
using PersistenceService.Data;

namespace PersistenceService.Consumers;

public class DocumentEnrichedConsumer : IConsumer<DocumentEnriched>
{
    private readonly AppDbContext _db;
    private readonly ILogger<DocumentEnrichedConsumer> _logger;

    public DocumentEnrichedConsumer(AppDbContext db, ILogger<DocumentEnrichedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentEnriched> context)
    {
        var message = context.Message;

        var result = new DocumentResult
        {
            Id = Guid.NewGuid(),
            DocumentId = message.DocumentId,
            BatchId = message.BatchId,
            DocumentType = message.DocumentType,
            Classification = message.Classification,
            ExtractedEntities = JsonSerializer.Serialize(message.ExtractedEntities),
            ConfidenceScore = message.ConfidenceScore,
            EnrichedAt = message.EnrichedAt,
            PersistedAt = DateTime.UtcNow,
            ProcessingCount = 1
        };

        _db.DocumentResults.Add(result);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Persisted document {DocumentId}", message.DocumentId);
    }
}
