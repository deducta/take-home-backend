namespace DocIntelligence.Contracts;

public record DocumentSubmitted
{
    public Guid DocumentId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty; // "invoice", "contract", "report"
    public Guid BatchId { get; init; }
    public DateTime SubmittedAt { get; init; }
}
