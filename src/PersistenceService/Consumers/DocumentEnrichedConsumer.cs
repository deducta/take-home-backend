using System.Text.Json;
using DocIntelligence.Contracts;
using MassTransit;
using PersistenceService.Data;

namespace PersistenceService.Consumers;

public class DocumentEnrichedConsumer : IConsumer<Batch<DocumentEnriched>>
{
    private readonly AppDbContext _db;
    private readonly ILogger<DocumentEnrichedConsumer> _logger;

    public DocumentEnrichedConsumer(AppDbContext db, ILogger<DocumentEnrichedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Batch<DocumentEnriched>> context)
    {
        var results = context.Message.Select(consumedMessage =>
        {
            var message = consumedMessage.Message;
            return new DocumentResult
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
        });

        _db.DocumentResults.AddRange(results);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Persisted documents with length {count}", context.Message.Length);
    }
}
