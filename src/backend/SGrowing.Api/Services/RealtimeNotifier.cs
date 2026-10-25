using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Conversations;
using Microsoft.AspNetCore.SignalR;

namespace SGrowing.Api.Services;

public interface IRealtimeNotifier
{
    Task NotifyNewMessageAsync(Guid tenantId, MessageDto message);
    Task NotifyConversationUpdatedAsync(Guid tenantId, ConversationDto conversation);
    Task NotifyAiDraftAsync(Guid tenantId, AiDraftDto draft);
}

public class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<OmniHub, IOmniClient> _hubContext;

    public RealtimeNotifier(IHubContext<OmniHub, IOmniClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyNewMessageAsync(Guid tenantId, MessageDto message)
    {
        // Broadcast to both the specific conversation room and the tenant room (for unread counts)
        await _hubContext.Clients.Group(OmniHub.GetConversationGroup(message.ConversationId)).OnMessageReceived(message);
        await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId)).OnMessageReceived(message);
    }

    public async Task NotifyConversationUpdatedAsync(Guid tenantId, ConversationDto conversation)
    {
        await _hubContext.Clients.Group(OmniHub.GetConversationGroup(conversation.Id)).OnConversationUpdated(conversation);
        await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId)).OnConversationUpdated(conversation);
    }

    public async Task NotifyAiDraftAsync(Guid tenantId, AiDraftDto draft)
    {
        await _hubContext.Clients.Group(OmniHub.GetConversationGroup(draft.ConversationId)).OnAiDraftGenerated(draft);
    }
}
