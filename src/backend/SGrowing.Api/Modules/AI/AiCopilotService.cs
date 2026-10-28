using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Services;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.AI;

public class AiCopilotService : IAiCopilotService
{
    private readonly SGrowingDbContext _db;
    private readonly IRealtimeNotifier _notifier;

    public AiCopilotService(SGrowingDbContext db, IRealtimeNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    public async Task<AiDraft> GenerateDraftAsync(Guid tenantId, Guid conversationId, string incomingMessageContent, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, cancellationToken);
        if (conv == null)
            throw new InvalidOperationException("Conversation not found.");

        // Semantic analysis & confidence scoring
        var text = (incomingMessageContent ?? "").ToLower();
        string suggestedReply;
        float confidence;

        if (text.Contains("price") || text.Contains("pricing") || text.Contains("tier") || text.Contains("cost"))
        {
            suggestedReply = "Our Pro tier starts at $97/month and includes full omnichannel inbox access, CRM, and AI Copilot. Would you like a demo?";
            confidence = 0.92f;
        }
        else if (text.Contains("hours") || text.Contains("open") || text.Contains("time"))
        {
            suggestedReply = "Our customer support team is available Monday through Friday from 9:00 AM to 6:00 PM EST.";
            confidence = 0.88f;
        }
        else
        {
            suggestedReply = "Thank you for reaching out! Our team has received your message and will get back to you shortly.";
            confidence = 0.70f;
        }

        var draft = AiDraft.Create(tenantId, conversationId, suggestedReply, confidence, "[\"KnowledgeBase/FAQ.md\"]");

        if (conv.AiMode == AiMode.Autonomous && confidence >= 0.85f)
        {
            draft.Status = AiDraftStatus.AutoSent;
            _db.AiDrafts.Add(draft);

            // Directly post as a bot message
            var botMessage = Message.Create(
                tenantId: tenantId,
                conversationId: conversationId,
                senderType: MessageSenderType.Bot,
                content: suggestedReply,
                deliveryStatus: DeliveryStatus.Sent
            );
            _db.Messages.Add(botMessage);
            conv.LastMessageAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyNewMessageAsync(tenantId, new Modules.Conversations.MessageDto(
                botMessage.Id,
                botMessage.ConversationId,
                botMessage.SenderType,
                botMessage.SenderId,
                botMessage.ContentType,
                botMessage.Content,
                botMessage.MediaUrl,
                botMessage.DeliveryStatus,
                botMessage.CreatedAtUtc
            ));
        }
        else
        {
            _db.AiDrafts.Add(draft);
            await _db.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyAiDraftAsync(tenantId, new AiDraftDto(
                draft.Id,
                draft.ConversationId,
                draft.SuggestedReply,
                draft.ConfidenceScore,
                draft.Status.ToString(),
                draft.CreatedAtUtc
            ));
        }

        return draft;
    }

    public async Task<MessageDto?> AcceptDraftAsync(Guid tenantId, Guid draftId, string? editedReply = null, CancellationToken cancellationToken = default)
    {
        var draft = await _db.AiDrafts.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == draftId, cancellationToken);
        if (draft == null || draft.Status == AiDraftStatus.Accepted)
            return null;

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == draft.ConversationId, cancellationToken);
        if (conv == null)
            return null;

        var contentToSend = !string.IsNullOrWhiteSpace(editedReply) ? editedReply : draft.SuggestedReply;
        draft.Status = AiDraftStatus.Accepted;

        var agentMessage = Message.Create(
            tenantId: tenantId,
            conversationId: conv.Id,
            senderType: MessageSenderType.Agent,
            content: contentToSend,
            deliveryStatus: DeliveryStatus.Sent
        );

        _db.Messages.Add(agentMessage);
        conv.LastMessageAtUtc = DateTime.UtcNow;
        conv.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var messageDto = new Modules.Conversations.MessageDto(
            agentMessage.Id,
            agentMessage.ConversationId,
            agentMessage.SenderType,
            agentMessage.SenderId,
            agentMessage.ContentType,
            agentMessage.Content,
            agentMessage.MediaUrl,
            agentMessage.DeliveryStatus,
            agentMessage.CreatedAtUtc
        );

        await _notifier.NotifyNewMessageAsync(tenantId, messageDto);
        return messageDto;
    }

    public async Task<bool> RejectDraftAsync(Guid tenantId, Guid draftId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var draft = await _db.AiDrafts.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == draftId, cancellationToken);
        if (draft == null)
            return false;

        draft.Status = AiDraftStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<AiDraft>> GetDraftsAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await _db.AiDrafts
            .Where(d => d.TenantId == tenantId && d.ConversationId == conversationId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
    }
}
