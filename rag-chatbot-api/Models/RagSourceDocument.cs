namespace rag_chatbot_api.Models;

public class RagSourceDocument
{
    public int Id { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime SourceUpdatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
