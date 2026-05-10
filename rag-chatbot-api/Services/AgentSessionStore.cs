using System.Collections.Concurrent;
using Microsoft.Agents.AI;

namespace rag_chatbot_api.Services;

public sealed class AgentSessionStore : IAgentSessionStore
{
    private readonly ConcurrentDictionary<Guid, AgentSession> _sessions = new();

    public bool TryGet(Guid chatSessionId, out AgentSession session)
    {
        return _sessions.TryGetValue(chatSessionId, out session!);
    }

    public void Set(Guid chatSessionId, AgentSession session)
    {
        _sessions[chatSessionId] = session;
    }

    public void Remove(Guid chatSessionId)
    {
        _sessions.TryRemove(chatSessionId, out _);
    }
}
