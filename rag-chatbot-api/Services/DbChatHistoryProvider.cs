using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using rag_chatbot_api.Data;

namespace rag_chatbot_api.Services;

public sealed class DbChatHistoryProvider(
    AppDbContext dbContext,
    Guid chatSessionId,
    int maxMessages = 20) : ChatHistoryProvider
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly Guid _chatSessionId = chatSessionId;
    private readonly int _maxMessages = Math.Clamp(maxMessages, 1, 100);

    protected override async ValueTask<IEnumerable<ChatMessage>> InvokingCoreAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.ChatSessionMessages
            .AsNoTracking()
            .Where(m => m.SessionId == _chatSessionId)
            .OrderByDescending(m => m.MessageOrder)
            .Take(_maxMessages)
            .Select(m => new
            {
                m.Role,
                m.Content
            })
            .ToListAsync(cancellationToken);

        messages.Reverse();

        var stampedChatHistory = messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new ChatMessage(MapRole(m.Role), m.Content!)
                .WithAgentRequestMessageSource(AgentRequestMessageSourceType.ChatHistory, GetType().FullName!));

        return stampedChatHistory.Concat(context.RequestMessages);
    }

    protected override ValueTask InvokedCoreAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.InvokeException is not null)
        {
            return default;
        }

        _ = context.RequestMessages
            .Where(m => m.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory);

        // ChatSessionService already persists user/assistant messages in this app.
        // Keep provider storage read-only to avoid duplicate DB writes.
        return default;
    }

    private static ChatRole MapRole(string? role)
    {
        return role?.ToLowerInvariant() switch
        {
            "assistant" => ChatRole.Assistant,
            "tool" => ChatRole.Tool,
            _ => ChatRole.User
        };
    }
}
