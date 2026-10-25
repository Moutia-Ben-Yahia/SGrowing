using MarketingStudio.Api.Data;
using MarketingStudio.Api.Hubs;
using MarketingStudio.Api.Modules.Conversations;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Common;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.AI;

public record ApproveAiDraftRequest(Guid TenantId, Guid DraftId, Guid? ApprovingAgentId = null);

public class ApproveAiDraftHandler
{
    private readonly MarketingStudioDbContext _db;
    private readonly IRealtimeNotifier _notifier;

    public ApproveAiDraftHandler(MarketingStudioDbContext db, IRealtimeNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    public async Task<Result<MessageDto>> HandleAsync(ApproveAiDraftRequest request, CancellationToken cancellationToken = default)
    {
        var draft = await _db.AiDrafts.FirstOrDefaultAsync(d => d.TenantId == request.TenantId && d.Id == request.DraftId, cancellationToken);
        if (draft == null)
            return Result<MessageDto>.Failure("AI Draft not found.");

        if (draft.Status != AiDraftStatus.Drafted)
            return Result<MessageDto>.Failure("Draft has already been processed.");

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == request.TenantId && c.Id == draft.ConversationId, cancellationToken);
        if (conv == null)
            return Result<MessageDto>.Failure("Conversation not found.");

        draft.Status = AiDraftStatus.Accepted;

        var message = Message.Create(
            tenantId: request.TenantId,
            conversationId: conv.Id,
            senderType: MessageSenderType.Agent,
            content: draft.SuggestedReply,
            senderId: request.ApprovingAgentId,
            deliveryStatus: DeliveryStatus.Sent
        );

        _db.Messages.Add(message);
        conv.LastMessageAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var dto = new MessageDto(
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

        await _notifier.NotifyNewMessageAsync(request.TenantId, dto);

        return Result<MessageDto>.Success(dto);
    }
}
