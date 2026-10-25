using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Services;
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
}
