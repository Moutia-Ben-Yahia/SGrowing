using SGrowing.Api.Data;
using SGrowing.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Conversations;

public class GetConversationMessagesHandler
{
    private readonly SGrowingDbContext _db;

    public GetConversationMessagesHandler(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<List<MessageDto>>> HandleAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var messages = await _db.Messages
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new MessageDto(
                m.Id,
                m.ConversationId,
                m.SenderType,
                m.SenderId,
                m.ContentType,
                m.Content,
                m.MediaUrl,
                m.DeliveryStatus,
                m.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Result<List<MessageDto>>.Success(messages);
    }
}
