namespace PersistenceService.Data;

public class DocumentResult
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid BatchId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string ExtractedEntities { get; set; } = "[]"; // JSON array stored as string
    public double ConfidenceScore { get; set; }
    public DateTime EnrichedAt { get; set; }
    public DateTime PersistedAt { get; set; }
    public int ProcessingCount { get; set; } // tracks how many times this was processed
}
