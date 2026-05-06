namespace rag_chatbot_api.Dtos.Admin;

public class AdminRagConfigurationResponse
{
    public string OpenAIBaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string EmbeddingModelId { get; set; } = string.Empty;
    public string OpenAIApiKey { get; set; } = string.Empty;
    public int TopK { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
