using MarketingStudio.Api.Data;
using MarketingStudio.Core.Common;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.Conversations;

public record ChangeStatusRequest(Guid TenantId, Guid ConversationId, ConversationStatus Status);

public record ConversationDto(
    Guid Id,
    Guid TenantId,
    Guid InboxId,
    Guid ContactId,
    Guid? AssigneeId,
    ConversationStatus Status,
    ConversationPriority Priority,
    AiMode AiMode,
    DateTime? LastMessageAtUtc,
    DateTime CreatedAtUtc
);

public class ChangeStatusHandler
{
    private readonly MarketingStudioDbContext _db;

    public ChangeStatusHandler(MarketingStudioDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ConversationDto>> HandleAsync(ChangeStatusRequest request, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == request.TenantId && c.Id == request.ConversationId, cancellationToken);
        if (conv == null)
            return Result<ConversationDto>.Failure("Conversation not found.");

        conv.ChangeStatus(request.Status);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<ConversationDto>.Success(new ConversationDto(
            conv.Id,
            conv.TenantId,
            conv.InboxId,
            conv.ContactId,
            conv.AssigneeId,
            conv.Status,
            conv.Priority,
            conv.AiMode,
            conv.LastMessageAtUtc,
            conv.CreatedAtUtc
        ));
    }
}
