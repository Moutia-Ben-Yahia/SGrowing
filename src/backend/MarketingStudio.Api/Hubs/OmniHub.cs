using Microsoft.AspNetCore.SignalR;

namespace MarketingStudio.Api.Hubs;

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

    public static string GetConversationGroup(Guid conversationId) => $"conv_{conversationId}";
    public static string GetTenantGroup(Guid tenantId) => $"tenant_{tenantId}";
}
