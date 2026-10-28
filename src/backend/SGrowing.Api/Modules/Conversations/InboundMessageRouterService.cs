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
    string? InstanceName = null,
    WhatsAppInteractiveCallbackPayload? InteractiveCallback = null
);

public record InboundRoutingResult(
    Guid ConversationId,
    Guid MessageId,
    Guid ContactId,
    bool IsNewConversation,
    string Status,
    string? AiSuggestedReply = null,
    Guid? AssignedOperatorId = null,
    string? TranscribedText = null
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
    private readonly IAudioTranscriptionService _transcriptionService;
    private readonly IOperatorAssignmentService _assignmentService;

    public InboundMessageRouterService(
        SGrowingDbContext db,
        IRealtimeNotifier notifier,
        IAiCopilotService copilotService,
        IAudioTranscriptionService? transcriptionService = null,
        IOperatorAssignmentService? assignmentService = null)
    {
        _db = db;
        _notifier = notifier;
        _copilotService = copilotService;
        _transcriptionService = transcriptionService ?? new AudioTranscriptionService();
        _assignmentService = assignmentService ?? new OperatorAssignmentService(db);
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
            await _db.SaveChangesAsync(ct);
            isNew = true;
        }

        // 4. Create Inbound Message Record with Audio Transcription & Callback parsing
        bool isAudio = payload.MediaUrl != null && (
            payload.MediaUrl.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
            payload.MediaUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
            payload.MediaUrl.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
            payload.MediaUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
            payload.MediaUrl.Contains("/audio/")
        );

        var contentType = isAudio
            ? MessageContentType.Audio
            : !string.IsNullOrWhiteSpace(payload.MediaUrl) ? MessageContentType.Image : MessageContentType.Text;

        string? detectedLanguage = null;
        string? transcribedText = null;
        string finalContent;

        if (isAudio)
        {
            var transcription = await _transcriptionService.TranscribeAudioAsync(payload.MediaUrl!, null, ct);
            detectedLanguage = transcription.DetectedLanguage;
            transcribedText = transcription.TranscribedText;
            finalContent = $"[Voice Note Transcribed ({transcription.DetectedLanguage})]: \"{transcription.TranscribedText}\"";
        }
        else if (payload.InteractiveCallback != null)
        {
            finalContent = $"[Selected: {payload.InteractiveCallback.ButtonText ?? payload.InteractiveCallback.ButtonId}] {payload.Content}".Trim();
        }
        else
        {
            finalContent = payload.Content;
        }

        var message = Message.Create(
            tenantId: payload.TenantId,
            conversationId: conversation.Id,
            senderType: MessageSenderType.Contact,
            content: finalContent,
            contentType: contentType,
            mediaUrl: payload.MediaUrl,
            externalMessageId: payload.ExternalMessageId ?? $"wa-inbound-{Guid.NewGuid():N}",
            deliveryStatus: DeliveryStatus.Delivered
        );

        _db.Messages.Add(message);

        // 5. Omnichannel Operator Auto-Assignment if unassigned
        Guid? assignedOperatorId = conversation.AssigneeId;
        if (assignedOperatorId == null)
        {
            var assignment = await _assignmentService.AssignConversationAsync(payload.TenantId, conversation.Id, detectedLanguage, ct);
            if (assignment != null)
            {
                assignedOperatorId = assignment.OperatorId;
                conversation.AssigneeId = assignment.OperatorId;
            }
        }

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
            AiSuggestedReply: suggestedReply,
            AssignedOperatorId: assignedOperatorId,
            TranscribedText: transcribedText
        );
    }
}
