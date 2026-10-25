using SGrowing.Api.Data;
using SGrowing.Api.Services;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Conversations;

public class SlaMonitoringService
{
    private readonly SGrowingDbContext _db;
    private readonly IRealtimeNotifier _notifier;

    public SlaMonitoringService(SGrowingDbContext db, IRealtimeNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    public async Task<int> EvaluateSlaBreachesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var openConversations = await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.Status == ConversationStatus.Open && !c.IsSlaBreached)
            .ToListAsync(cancellationToken);

        if (openConversations.Count == 0) return 0;

        var policies = await _db.SlaPolicies
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToListAsync(cancellationToken);

        int breachedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var conv in openConversations)
        {
            var policy = policies.FirstOrDefault(p => p.Priority == conv.Priority)
                         ?? policies.FirstOrDefault();

            if (policy == null) continue;

            // Check First Response Time (FRT)
            if (conv.FirstResponseAtUtc == null)
            {
                var elapsedMinutes = (now - conv.CreatedAtUtc).TotalMinutes;
                if (elapsedMinutes >= policy.TargetFirstResponseMinutes)
                {
                    conv.IsSlaBreached = true;
                    conv.Priority = ConversationPriority.Urgent;
                    breachedCount++;

                    await _notifier.NotifyConversationUpdatedAsync(tenantId, new ConversationDto(
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
        }

        if (breachedCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return breachedCount;
    }
}
