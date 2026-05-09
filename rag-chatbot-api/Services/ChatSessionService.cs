using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.Text.Json;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.ChatSession;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;

#pragma warning disable SKEXP0001

namespace rag_chatbot_api.Services;

public class ChatSessionService(
    AppDbContext dbContext,
    IOptions<RagOptions> ragOptions,
    ILogger<ChatSessionService> logger) : IChatSessionService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly RagOptions _ragOptions = ragOptions.Value;
    private readonly ILogger<ChatSessionService> _logger = logger;

    public async Task<ChatSession> CreateSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Topic = "New Chat",
            IsCustomTopic = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return session;
    }

    public async Task<ChatSessionResponse?> GetSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.UserId == userId && s.DeletedAtUtc == null)
            .Select(s => new ChatSessionResponse
            {
                Id = s.Id,
                Topic = s.Topic,
                IsPinned = s.IsPinned,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                MessageCount = s.Messages.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        return session;
    }

    public async Task<ChatSessionDetailResponse?> GetSessionDetailAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.UserId == userId && s.DeletedAtUtc == null)
            .Include(s => s.Messages.OrderBy(m => m.MessageOrder))
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
            return null;

        var messages = session.Messages
            .Select(m => new ChatSessionMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                Sources = m.Sources != null ? JsonSerializer.Deserialize<List<ChatMessageSource>>(m.Sources) : null,
                CreatedAtUtc = m.CreatedAtUtc
            })
            .ToList();

        return new ChatSessionDetailResponse
        {
            Id = session.Id,
            Topic = session.Topic,
            IsPinned = session.IsPinned,
            CreatedAtUtc = session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc,
            Messages = messages
        };
    }

    public async Task<List<ChatSessionResponse>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.DeletedAtUtc == null)
            .OrderByDescending(s => s.IsPinned)
            .ThenByDescending(s => s.UpdatedAtUtc)
            .Select(s => new ChatSessionResponse
            {
                Id = s.Id,
                Topic = s.Topic,
                IsPinned = s.IsPinned,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                MessageCount = s.Messages.Count
            })
            .ToListAsync(cancellationToken);

        return sessions;
    }

    public async Task<bool> RenameSessionAsync(Guid sessionId, Guid userId, string newTopic, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newTopic))
            return false;

        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.DeletedAtUtc == null, cancellationToken);

        if (session == null)
            return false;

        session.Topic = newTopic.Trim();
        session.IsCustomTopic = true;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> PinSessionAsync(Guid sessionId, Guid userId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.DeletedAtUtc == null, cancellationToken);

        if (session == null)
            return false;

        session.IsPinned = isPinned;
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.DeletedAtUtc == null, cancellationToken);

        if (session == null)
            return false;

        session.DeletedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddMessageToSessionAsync(Guid sessionId, string role, string content, List<ChatMessageSource>? sources, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.DeletedAtUtc == null, cancellationToken);

        if (session == null)
            return;

        var messageOrder = await _dbContext.ChatSessionMessages
            .CountAsync(m => m.SessionId == sessionId, cancellationToken);

        var now = DateTime.UtcNow;
        var sourcesJson = sources != null ? JsonSerializer.Serialize(sources) : null;

        var message = new ChatSessionMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = role,
            Content = content,
            Sources = sourcesJson,
            CreatedAtUtc = now,
            MessageOrder = messageOrder
        };

        _dbContext.ChatSessionMessages.Add(message);
        session.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GenerateTopicAsync(string question, string answer, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _dbContext.RagRuntimeConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (config == null)
                return ExtractTopicFromQuestion(question);

            var kernel = KernelFactory.CreateKernel(config, out var settings);

            if (!settings.HasRequiredConfiguration)
                return ExtractTopicFromQuestion(question);

            var prompt = $@"Generate a short, concise topic title (max 50 characters) for a chat based on the user's question and the assistant's answer.

User Question: {question}
Assistant Answer: {answer}

Respond with ONLY the topic title, no additional text.";

            var response = await kernel.InvokePromptAsync<string>(prompt, cancellationToken: cancellationToken);
            var topic = response?.Trim() ?? ExtractTopicFromQuestion(question);

            return topic.Length > 50 ? topic.Substring(0, 47) + "..." : topic;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate topic with AI, using fallback");
            return ExtractTopicFromQuestion(question);
        }
    }

    private static string ExtractTopicFromQuestion(string question)
    {
        var words = question.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var topic = string.Join(" ", words.Take(5));
        return topic.Length > 50 ? topic.Substring(0, 47) + "..." : topic;
    }
}
