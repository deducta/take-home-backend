using System.Net.Http.Json;

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5100") };

Console.WriteLine("Sending 10,000 documents in a spike...");

// Simulate flash spike: 10k documents across 50 concurrent batches of 200
var tasks = Enumerable.Range(0, 50).Select(async batchNum =>
{
    var documents = Enumerable.Range(0, 200).Select(i => new
    {
        Content = $"Document content for batch {batchNum}, item {i}. " +
                  string.Join(" ", Enumerable.Repeat("Lorem ipsum dolor sit amet.", 10)),
        Type = (i % 3) switch
        {
            0 => "invoice",
            1 => "contract",
            _ => "report"
        }
    }).ToList();

    var response = await httpClient.PostAsJsonAsync("/api/documents/batch",
        new { Documents = documents });

    Console.WriteLine($"Batch {batchNum}: {response.StatusCode}");
});

await Task.WhenAll(tasks);
Console.WriteLine("All batches submitted. Monitor RabbitMQ at http://localhost:15672");
