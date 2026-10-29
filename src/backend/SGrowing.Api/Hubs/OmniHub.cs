using Microsoft.AspNetCore.SignalR;

namespace SGrowing.Api.Hubs;

public class OmniHub : Hub<IOmniClient>
{
    public async Task JoinConversation(Guid conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
    }

    public async Task BroadcastTyping(Guid conversationId, string senderName, bool isTyping)
    {
        await Clients.Group(GetConversationGroup(conversationId))
            .OnTypingIndicator(conversationId, senderName, isTyping);
    }

    public async Task LockConversation(Guid conversationId, string agentName)
    {
        await Clients.Group(GetConversationGroup(conversationId))
            .OnConversationLocked(conversationId, agentName);
    }

    public async Task UnlockConversation(Guid conversationId)
    {
        await Clients.Group(GetConversationGroup(conversationId))
            .OnConversationUnlocked(conversationId);
    }

    public async Task BroadcastPresence(Guid conversationId, string operatorId, string operatorName, bool isTyping)
    {
        await Clients.Group(GetConversationGroup(conversationId))
            .OnOperatorPresenceChanged(conversationId, operatorId, operatorName, isTyping);
    }

    public async Task BroadcastSlaAlert(Guid tenantId, Guid conversationId, string priority, int minutesRemaining, bool isBreached)
    {
        await Clients.Group(GetTenantGroup(tenantId))
            .OnSlaBreachAlert(conversationId, priority, minutesRemaining, isBreached);
    }

    public async Task BroadcastCallState(Guid tenantId, Guid callSessionId, string status, string? callerPhone)
    {
        await Clients.Group(GetTenantGroup(tenantId))
            .OnCallSessionStateChanged(callSessionId, status, callerPhone);
    }

    public async Task BroadcastOrderLock(Guid tenantId, Guid orderId, string lockedByAgentId, string lockedByAgentName, DateTime expiresAtUtc)
    {
        await Clients.Group(GetTenantGroup(tenantId))
            .OnOrderLocked(orderId, lockedByAgentId, lockedByAgentName, expiresAtUtc);
    }

    public async Task BroadcastOrderUnlock(Guid tenantId, Guid orderId)
    {
        await Clients.Group(GetTenantGroup(tenantId))
            .OnOrderUnlocked(orderId);
    }

    public async Task BroadcastAndroidSmsStatus(Guid tenantId, Guid dispatchId, string status, string? errorMessage)
    {
        await Clients.Group(GetTenantGroup(tenantId))
            .OnAndroidSmsStatusUpdated(dispatchId, status, errorMessage);
    }

    public async Task BroadcastVoiceTranscription(Guid tenantId, Guid callSessionId, string speaker, string chunkText, double sentimentScore, bool isFinal)
    {
        await Clients.Group(GetTenantGroup(tenantId))
            .OnVoiceTranscriptionChunk(callSessionId, speaker, chunkText, sentimentScore, isFinal);
    }

    public async Task BroadcastHighIntentProposalViewed(HighIntentProposalViewedEventDto alert)
    {
        await Clients.Group(GetTenantGroup(alert.TenantId))
            .OnHighIntentProposalViewed(alert);
    }

    public static string GetConversationGroup(Guid conversationId) => $"conv_{conversationId}";
    public static string GetTenantGroup(Guid tenantId) => $"tenant_{tenantId}";
}
