namespace rag_chatbot_api.Models;

public class RagRuntimeConfiguration
{
    public int Id { get; set; } = 1;
    public string OpenAIBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ModelId { get; set; } = "gpt-4o-mini";
    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";
    public string OpenAIApiKey { get; set; } = string.Empty;
    public int TopK { get; set; } = 3;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
