using rag_chatbot_api.Dtos.ChatSession;
using rag_chatbot_api.Models;

namespace rag_chatbot_api.Services;

public interface IChatSessionService
{
    Task<ChatSession> CreateSessionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ChatSessionResponse?> GetSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
    Task<ChatSessionDetailResponse?> GetSessionDetailAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
    Task<string> BuildSessionAwareQuestionAsync(Guid sessionId, Guid userId, string question, CancellationToken cancellationToken = default);
    Task<List<ChatSessionResponse>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RenameSessionAsync(Guid sessionId, Guid userId, string newTopic, CancellationToken cancellationToken = default);
    Task<bool> PinSessionAsync(Guid sessionId, Guid userId, bool isPinned, CancellationToken cancellationToken = default);
    Task<bool> DeleteSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
    Task<string> GenerateTopicAsync(string question, string answer, CancellationToken cancellationToken = default);
    Task AddMessageToSessionAsync(Guid sessionId, string role, string content, List<ChatMessageSource>? sources, CancellationToken cancellationToken = default);
}
