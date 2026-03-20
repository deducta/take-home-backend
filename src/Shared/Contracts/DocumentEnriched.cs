namespace DocIntelligence.Contracts;

public record DocumentEnriched
{
    public Guid DocumentId { get; init; }
    public Guid BatchId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public List<string> ExtractedEntities { get; init; } = new();
    public double ConfidenceScore { get; init; }
    public DateTime EnrichedAt { get; init; }
}
