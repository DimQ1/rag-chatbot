namespace rag_chatbot_api.Options;

public class RagOptions
{
    public const string SectionName = "Rag";

    public string OpenAIBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ModelId { get; set; } = "gpt-4o-mini";
    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";
    public string OpenAIApiKey { get; set; } = string.Empty;
    public int TopK { get; set; } = 3;
}
