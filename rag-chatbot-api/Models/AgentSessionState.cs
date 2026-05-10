namespace rag_chatbot_api.Models;

public class AgentSessionState
{
    public Guid ChatSessionId { get; set; }
    public string SerializedSession { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
