using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations.PresenceAndCsat;

public class ConversationPresenceService : IConversationPresenceService
{
    private readonly SGrowingDbContext _db;

    public ConversationPresenceService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationPresenceReport> HeartbeatPresenceAsync(
        Guid tenantId,
        Guid conversationId,
        Guid agentUserId,
        string agentName,
        bool isTyping)
    {
        var now = DateTime.UtcNow;
        var session = await _db.ConversationPresenceSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ConversationId == conversationId && s.AgentUserId == agentUserId);

        if (session == null)
        {
            session = ConversationPresenceSession.Create(tenantId, conversationId, agentUserId, agentName, isTyping);
            _db.ConversationPresenceSessions.Add(session);
        }
        else
        {
            session.Heartbeat(isTyping);
            session.AgentName = agentName.Trim();
        }

        await _db.SaveChangesAsync();

        return await GetActivePresenceAsync(tenantId, conversationId);
    }

    public async Task<bool> LeavePresenceAsync(Guid tenantId, Guid conversationId, Guid agentUserId)
    {
        var session = await _db.ConversationPresenceSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ConversationId == conversationId && s.AgentUserId == agentUserId && s.IsActive);

        if (session == null) return false;

        session.Leave();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ConversationPresenceReport> GetActivePresenceAsync(Guid tenantId, Guid conversationId)
    {
        var now = DateTime.UtcNow;
        var activeSessions = await _db.ConversationPresenceSessions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.ConversationId == conversationId && s.IsActive && s.ExpiresAtUtc > now)
            .OrderByDescending(s => s.LastHeartbeatUtc)
            .ToListAsync();

        var agentDtos = activeSessions.Select(s => new ActiveAgentDto(
            s.AgentUserId,
            s.AgentName,
            s.IsTyping,
            s.LastHeartbeatUtc)).ToList();

        var hasCollision = agentDtos.Count > 1;
        string? warning = null;
        if (hasCollision)
        {
            var otherAgents = string.Join(", ", agentDtos.Select(a => a.AgentName));
            var typing = agentDtos.Any(a => a.IsTyping) ? " (someone is typing)" : "";
            warning = $"⚠️ Collision Warning: Multiple agents viewing this ticket ({otherAgents}){typing}.";
        }

        return new ConversationPresenceReport(
            conversationId,
            hasCollision,
            warning,
            agentDtos);
    }
}
