namespace rag_chatbot_api.Services;

public interface IAgentSessionStateStore
{
    Task<string?> LoadAsync(Guid chatSessionId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid chatSessionId, string serializedSession, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid chatSessionId, CancellationToken cancellationToken = default);
}
