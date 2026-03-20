namespace DocIntelligence.Contracts;

public record EnrichmentResult
{
    public string Classification { get; init; } = string.Empty;
    public string[] Entities { get; init; } = Array.Empty<string>();
    public double Confidence { get; init; }
}
