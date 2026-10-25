using SGrowing.Api.Data;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Services;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.AI;

public record AutonomousProcessResult(bool Processed, string? ReplySent, bool EscalatedToHuman);

public class AutonomousAiAgentService
{
    private readonly SGrowingDbContext _db;
    private readonly IEnumerable<IAgentTool> _tools;
    private readonly IRealtimeNotifier _notifier;

    public AutonomousAiAgentService(
        SGrowingDbContext db,
        IEnumerable<IAgentTool> tools,
        IRealtimeNotifier notifier)
    {
        _db = db;
        _tools = tools;
        _notifier = notifier;
    }

    public async Task<Result<AutonomousProcessResult>> HandleIncomingMessageAsync(
        Guid tenantId,
        Guid conversationId,
        Guid contactId,
        string customerMessage,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, cancellationToken);

        if (conversation == null)
            return Result<AutonomousProcessResult>.Failure("Conversation not found.");

        // Only process autonomously if AiMode is Autonomous
        if (conversation.AiMode != AiMode.Autonomous)
        {
            return Result<AutonomousProcessResult>.Success(new AutonomousProcessResult(false, null, false));
        }

        var normalized = customerMessage.Trim().ToLowerInvariant();
        string botReplyText = "";
        bool escalated = false;

        // 1. Check for explicit human handoff requests
        if (normalized.Contains("human") || normalized.Contains("agent") || normalized.Contains("person") || normalized.Contains("representative") || normalized.Contains("operator"))
        {
            var escalateTool = _tools.FirstOrDefault(t => t.Name == "EscalateToHuman");
            if (escalateTool != null)
            {
                await escalateTool.ExecuteAsync(tenantId, contactId, "{}", cancellationToken);
            }

            conversation.AiMode = AiMode.Copilot;
            conversation.Priority = ConversationPriority.High;
            botReplyText = "I have connected you with our human support team. A specialist has been notified and will reply shortly.";
            escalated = true;
        }
        // 2. Check for order / account queries
        else if (normalized.Contains("order") || normalized.Contains("deal") || normalized.Contains("purchase") || normalized.Contains("invoice"))
        {
            var orderTool = _tools.FirstOrDefault(t => t.Name == "OrderLookup");
            if (orderTool != null)
            {
                var toolRes = await orderTool.ExecuteAsync(tenantId, contactId, "{}", cancellationToken);
                botReplyText = toolRes.OutputData;
            }
            else
            {
                botReplyText = "I'm checking our records for your account details.";
            }
        }
        // 3. General technical / documentation question -> KnowledgeBaseTool
        else
        {
            var kbTool = _tools.FirstOrDefault(t => t.Name == "KnowledgeBaseSearch");
            if (kbTool != null)
            {
                var toolRes = await kbTool.ExecuteAsync(tenantId, contactId, customerMessage, cancellationToken);
                if (toolRes.Success && !toolRes.RequiresHumanEscalation)
                {
                    botReplyText = toolRes.OutputData;
                }
                else
                {
                    // Automatic graceful fallback to human
                    conversation.AiMode = AiMode.Copilot;
                    conversation.Priority = ConversationPriority.High;
                    botReplyText = "I wasn't able to locate a complete answer in our knowledge base, so I've transferred your question to our support team.";
                    escalated = true;
                }
            }
        }

        // Create Bot Message
        var botMessage = Message.Create(
            tenantId: tenantId,
            conversationId: conversationId,
            senderType: MessageSenderType.Bot,
            content: botReplyText,
            contentType: MessageContentType.Text,
            senderId: null
        );
        _db.Messages.Add(botMessage);
        conversation.LastMessageAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Broadcast to Realtime OmniHub
        var messageDto = new MessageDto(
            botMessage.Id,
            botMessage.ConversationId,
            botMessage.SenderType,
            botMessage.SenderId,
            botMessage.ContentType,
            botMessage.Content,
            botMessage.MediaUrl,
            botMessage.DeliveryStatus,
            botMessage.CreatedAtUtc
        );
        await _notifier.NotifyNewMessageAsync(tenantId, messageDto);

        return Result<AutonomousProcessResult>.Success(new AutonomousProcessResult(true, botReplyText, escalated));
    }
}
