namespace rag_chatbot_api.Dtos.Admin;

public class AdminLogQueryRequest
{
    public string? Search { get; init; }
    public string? Level { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
