namespace rag_chatbot_api.Dtos.Rag;

public class RagQueryResponse
{
    public string Answer { get; set; } = string.Empty;
    public IEnumerable<RagSource> Sources { get; set; } = [];
}

public class RagSource
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
