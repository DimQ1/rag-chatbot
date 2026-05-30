namespace rag_chatbot_api.Dtos.Admin;

public class AdminLogQueryResponse
{
    public IReadOnlyList<AdminLogEntryResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
