using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations.PresenceAndCsat;

public record ActiveAgentDto(
    Guid AgentUserId,
    string AgentName,
    bool IsTyping,
    DateTime LastHeartbeatUtc
);

public record ConversationPresenceReport(
    Guid ConversationId,
    bool HasCollision,
    string? CollisionWarningMessage,
    List<ActiveAgentDto> ActiveAgents
);

public interface IConversationPresenceService
{
    Task<ConversationPresenceReport> HeartbeatPresenceAsync(
        Guid tenantId,
        Guid conversationId,
        Guid agentUserId,
        string agentName,
        bool isTyping);

    Task<bool> LeavePresenceAsync(
        Guid tenantId,
        Guid conversationId,
        Guid agentUserId);

    Task<ConversationPresenceReport> GetActivePresenceAsync(
        Guid tenantId,
        Guid conversationId);
}
