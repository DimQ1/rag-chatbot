using Microsoft.EntityFrameworkCore;
using rag_chatbot_api.Data;
using rag_chatbot_api.Models;

namespace rag_chatbot_api.Services;

public sealed class DbAgentSessionStateStore(AppDbContext dbContext) : IAgentSessionStateStore
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<string?> LoadAsync(Guid chatSessionId, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.AgentSessionStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChatSessionId == chatSessionId, cancellationToken);

        return state?.SerializedSession;
    }

    public async Task SaveAsync(Guid chatSessionId, string serializedSession, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AgentSessionStates
            .FirstOrDefaultAsync(s => s.ChatSessionId == chatSessionId, cancellationToken);

        if (existing is null)
        {
            _dbContext.AgentSessionStates.Add(new AgentSessionState
            {
                ChatSessionId = chatSessionId,
                SerializedSession = serializedSession,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.SerializedSession = serializedSession;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid chatSessionId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AgentSessionStates
            .FirstOrDefaultAsync(s => s.ChatSessionId == chatSessionId, cancellationToken);

        if (existing is null)
        {
            return;
        }

        _dbContext.AgentSessionStates.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
