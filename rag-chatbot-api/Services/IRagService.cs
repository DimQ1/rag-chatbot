using rag_chatbot_api.Dtos.Rag;

namespace rag_chatbot_api.Services;

public interface IRagService
{
    Task<RagQueryResponse> QueryAsync(string question, Guid? chatSessionId = null, bool includeReasoning = false, CancellationToken cancellationToken = default);
}
