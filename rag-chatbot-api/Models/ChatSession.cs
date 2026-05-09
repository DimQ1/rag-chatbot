namespace rag_chatbot_api.Models;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Topic { get; set; } = "New Chat";
    public bool IsCustomTopic { get; set; } = false;
    public bool IsPinned { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }

    // Navigation properties
    public AppUser? User { get; set; }
    public ICollection<ChatSessionMessage> Messages { get; set; } = [];
}

public class ChatSessionMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public string? Sources { get; set; } // JSON array of sources
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int MessageOrder { get; set; }

    // Navigation property
    public ChatSession? Session { get; set; }
}
