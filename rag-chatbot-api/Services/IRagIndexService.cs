namespace rag_chatbot_api.Services;

public interface IRagIndexService
{
    Task<(int ProcessedCount, int RemovedCount)> ReprocessAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ReprocessDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task RemoveDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}
