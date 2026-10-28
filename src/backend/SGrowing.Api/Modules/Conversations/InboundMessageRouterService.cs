using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Conversations;

public record InboundMessagePayload(
    Guid TenantId,
    string PhoneNumber,
    string? SenderName,
    string Content,
    string ChannelType = "WhatsApp",
    string? ExternalMessageId = null,
    string? MediaUrl = null,
    string? InstanceName = null
);

public record InboundRoutingResult(
    Guid ConversationId,
    Guid MessageId,
    Guid ContactId,
    bool IsNewConversation,
    string Status,
    string? AiSuggestedReply = null
);

public interface IInboundMessageRouterService
{
    Task<InboundRoutingResult> RouteInboundMessageAsync(InboundMessagePayload payload, CancellationToken ct = default);
}

public class InboundMessageRouterService : IInboundMessageRouterService
{
    private readonly SGrowingDbContext _db;
    private readonly IRealtimeNotifier _notifier;
    private readonly IAiCopilotService _copilotService;

    public InboundMessageRouterService(
        SGrowingDbContext db,
        IRealtimeNotifier notifier,
        IAiCopilotService copilotService)
    {
        _db = db;
        _notifier = notifier;
        _copilotService = copilotService;
    }

    public async Task<InboundRoutingResult> RouteInboundMessageAsync(InboundMessagePayload payload, CancellationToken ct = default)
    {
        if (payload.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(payload));

        if (string.IsNullOrWhiteSpace(payload.PhoneNumber))
            throw new ArgumentException("PhoneNumber is required.", nameof(payload));

        var cleanPhone = Regex.Replace(payload.PhoneNumber, @"[^\d+]", "");

        // 1. Resolve or Create Contact
        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == payload.TenantId && (c.Phone == cleanPhone || c.Phone == payload.PhoneNumber), ct);

        if (contact == null)
        {
            var displayName = !string.IsNullOrWhiteSpace(payload.SenderName) ? payload.SenderName : "WhatsApp User";
            var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = parts.Length > 0 ? parts[0] : "WhatsApp";
            var lastName = parts.Length > 1 ? parts[1] : cleanPhone;

            contact = Contact.Create(
                payload.TenantId,
                firstName,
                lastName,
                phone: cleanPhone,
                stage: LifecycleStage.Lead,
                tags: new List<string> { "whatsapp-inbound", "automated-lead" }
            );

            _db.Contacts.Add(contact);
            await _db.SaveChangesAsync(ct);
        }

        // 2. Resolve or Create Inbox for WhatsApp
        var inbox = await _db.Inboxes
            .FirstOrDefaultAsync(i => i.TenantId == payload.TenantId && i.IsActive, ct);

        if (inbox == null)
        {
            inbox = new Inbox
            {
                Id = Guid.NewGuid(),
                TenantId = payload.TenantId,
                Name = "WhatsApp Gateway Inbox",
                ChannelType = ChannelType.WhatsAppEvolution,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Inboxes.Add(inbox);
            await _db.SaveChangesAsync(ct);
        }

        // 3. Resolve or Create Active Conversation
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == payload.TenantId && c.ContactId == contact.Id && c.Status != ConversationStatus.Resolved, ct);

        bool isNew = false;
        if (conversation == null)
        {
            conversation = Conversation.Create(payload.TenantId, inbox.Id, contact.Id, AiMode.Copilot);
            _db.Conversations.Add(conversation);
            isNew = true;
        }

        // 4. Create Inbound Message Record
        var contentType = !string.IsNullOrWhiteSpace(payload.MediaUrl) ? MessageContentType.Image : MessageContentType.Text;
        var message = Message.Create(
            tenantId: payload.TenantId,
            conversationId: conversation.Id,
            senderType: MessageSenderType.Contact,
            content: payload.Content,
            contentType: contentType,
            mediaUrl: payload.MediaUrl,
            externalMessageId: payload.ExternalMessageId ?? $"wa-inbound-{Guid.NewGuid():N}",
            deliveryStatus: DeliveryStatus.Delivered
        );

        _db.Messages.Add(message);
        conversation.LastMessageAtUtc = DateTime.UtcNow;
        conversation.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // 5. SignalR Real-Time Broadcasting to Inbox
        var messageDto = new MessageDto(
            message.Id,
            message.ConversationId,
            message.SenderType,
            message.SenderId,
            message.ContentType,
            message.Content,
            message.MediaUrl,
            message.DeliveryStatus,
            message.CreatedAtUtc
        );

        var convDto = new ConversationDto(
            conversation.Id,
            conversation.TenantId,
            conversation.InboxId,
            conversation.ContactId,
            conversation.AssigneeId,
            conversation.Status,
            conversation.Priority,
            conversation.AiMode,
            conversation.LastMessageAtUtc,
            conversation.CreatedAtUtc
        );

        await _notifier.NotifyNewMessageAsync(payload.TenantId, messageDto);
        await _notifier.NotifyConversationUpdatedAsync(payload.TenantId, convDto);

        // 6. Sub-second AI Copilot Suggested Reply Generation
        string? suggestedReply = null;
        if (conversation.AiMode != AiMode.Disabled)
        {
            try
            {
                var draft = await _copilotService.GenerateDraftAsync(payload.TenantId, conversation.Id, payload.Content, ct);
                suggestedReply = draft?.SuggestedReply;
            }
            catch
            {
                // Copilot failure must never fail the inbound message ingestion
            }
        }

        return new InboundRoutingResult(
            ConversationId: conversation.Id,
            MessageId: message.Id,
            ContactId: contact.Id,
            IsNewConversation: isNew,
            Status: "Routed",
            AiSuggestedReply: suggestedReply
        );
    }
}
