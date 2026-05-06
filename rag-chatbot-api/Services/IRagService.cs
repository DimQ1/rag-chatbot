using rag_chatbot_api.Dtos.Rag;

namespace rag_chatbot_api.Services;

public interface IRagService
{
    Task<RagQueryResponse> QueryAsync(string question, CancellationToken cancellationToken = default);
}
