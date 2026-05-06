namespace rag_chatbot_api.Dtos.Admin;

public class AdminDocumentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
