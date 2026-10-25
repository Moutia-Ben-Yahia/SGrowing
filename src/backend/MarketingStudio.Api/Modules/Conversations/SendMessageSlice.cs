using MarketingStudio.Api.Data;
using MarketingStudio.Core.Common;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.Conversations;

public record SendMessageRequest(
    Guid TenantId,
    Guid ConversationId,
    MessageSenderType SenderType,
    Guid? SenderId,
    MessageContentType ContentType,
    string Content,
    string? MediaUrl = null,
    string? ExternalMessageId = null
);

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    MessageSenderType SenderType,
    Guid? SenderId,
    MessageContentType ContentType,
    string Content,
    string? MediaUrl,
    DeliveryStatus DeliveryStatus,
    DateTime CreatedAtUtc
);

public class SendMessageHandler
{
    private readonly MarketingStudioDbContext _db;

    public SendMessageHandler(MarketingStudioDbContext db)
    {
        _db = db;
    }

    public async Task<Result<MessageDto>> HandleAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == request.TenantId && c.Id == request.ConversationId, cancellationToken);
        if (conv == null)
            return Result<MessageDto>.Failure("Conversation not found.");

        var message = Message.Create(
            tenantId: request.TenantId,
            conversationId: request.ConversationId,
            senderType: request.SenderType,
            content: request.Content,
            senderId: request.SenderId,
            contentType: request.ContentType,
            mediaUrl: request.MediaUrl,
            externalMessageId: request.ExternalMessageId,
            deliveryStatus: DeliveryStatus.Sent
        );

        _db.Messages.Add(message);

        conv.LastMessageAtUtc = DateTime.UtcNow;
        conv.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<MessageDto>.Success(new MessageDto(
            message.Id,
            message.ConversationId,
            message.SenderType,
            message.SenderId,
            message.ContentType,
            message.Content,
            message.MediaUrl,
            message.DeliveryStatus,
            message.CreatedAtUtc
        ));
    }
}
