namespace rag_chatbot_api.Dtos.ChatSession;

public class ChatSessionResponse
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int MessageCount { get; set; }
}

public class ChatSessionDetailResponse
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<ChatSessionMessageDto> Messages { get; set; } = [];
}

public class ChatSessionMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<ChatMessageSource>? Sources { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ChatMessageSource
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class CreateChatSessionRequest
{
    public string? InitialQuestion { get; set; }
}

public class RenameChatSessionRequest
{
    public string Topic { get; set; } = string.Empty;
}

public class PinChatSessionRequest
{
    public bool IsPinned { get; set; }
}

public class AddMessageToChatSessionRequest
{
    public Guid SessionId { get; set; }
    public string Question { get; set; } = string.Empty;
}
