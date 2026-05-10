using Microsoft.Agents.AI;

namespace rag_chatbot_api.Services;

public interface IAgentSessionStore
{
    bool TryGet(Guid chatSessionId, out AgentSession session);
    void Set(Guid chatSessionId, AgentSession session);
    void Remove(Guid chatSessionId);
}
