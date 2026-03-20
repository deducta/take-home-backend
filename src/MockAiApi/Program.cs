using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Simulate rate limiting: track requests per second
var requestTimestamps = new ConcurrentQueue<DateTime>();
const int RateLimitPerSecond = 20;

app.MapPost("/api/enrich", async (EnrichRequest request) =>
{
    // Clean old timestamps
    while (requestTimestamps.TryPeek(out var oldest) && oldest < DateTime.UtcNow.AddSeconds(-1))
        requestTimestamps.TryDequeue(out _);

    requestTimestamps.Enqueue(DateTime.UtcNow);

    // Rate limit: return 429 if over limit
    if (requestTimestamps.Count > RateLimitPerSecond)
    {
        return Results.StatusCode(429);
    }

    // Simulate transient failures (~10% of requests)
    if (Random.Shared.NextDouble() < 0.10)
    {
        return Results.StatusCode(503);
    }

    // Simulate processing latency (2-5 seconds)
    await Task.Delay(Random.Shared.Next(2000, 5000));

    // Return mock enrichment result
    return Results.Ok(new EnrichResponse
    {
        Classification = request.DocumentType switch
        {
            "invoice" => "financial_document",
            "contract" => "legal_document",
            _ => "general_document"
        },
        Entities = new[] { "Entity_A", "Entity_B", $"Entity_{Random.Shared.Next(100)}" },
        Confidence = 0.7 + Random.Shared.NextDouble() * 0.3
    });
});

app.Run("http://0.0.0.0:5050");

public record EnrichRequest(string Content, string DocumentType);
public record EnrichResponse
{
    public string Classification { get; init; } = "";
    public string[] Entities { get; init; } = Array.Empty<string>();
    public double Confidence { get; init; }
}
