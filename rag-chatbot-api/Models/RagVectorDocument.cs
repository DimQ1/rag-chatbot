namespace rag_chatbot_api.Models;

public class RagVectorDocument
{
    public int Id { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string? EmbeddingJson { get; set; }
    public string? EmbeddingModelId { get; set; }
    public DateTime SourceUpdatedAtUtc { get; set; }
    public DateTime IndexedAtUtc { get; set; }
}
