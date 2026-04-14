using DocIntelligence.Contracts;
using MassTransit;
using System.Linq;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/documents/batch", async (
            BatchRequest request,
            IPublishEndpoint publishEndpoint) =>
        {
            var batchId = Guid.NewGuid();

            var documents =
                request.Documents.Select(doc => new DocumentSubmitted
                {
                    DocumentId = Guid.NewGuid(),
                    Content = doc.Content,
                    DocumentType = doc.Type,
                    BatchId = batchId,
                    SubmittedAt = DateTime.UtcNow
                }).ToList();
            
            await publishEndpoint.PublishBatch(documents);

            return Results.Accepted(value: new { BatchId = batchId, Count = request.Documents.Count });
        });
    }
}

public record BatchRequest(List<DocumentInput> Documents);
public record DocumentInput(string Content, string Type);