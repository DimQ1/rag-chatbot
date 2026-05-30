namespace rag_chatbot_api.Dtos.Admin;

public class AdminLogEntryResponse
{
    public long Id { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
    public int EventId { get; init; }
    public string? EventName { get; init; }
    public string? TraceId { get; init; }
    public string? RequestPath { get; init; }
    public string? RequestMethod { get; init; }
    public string? UserId { get; init; }
}
