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

    public static string GetConversationGroup(Guid conversationId) => $"conv_{conversationId}";
    public static string GetTenantGroup(Guid tenantId) => $"tenant_{tenantId}";
}
