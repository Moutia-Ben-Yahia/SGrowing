using MarketingStudio.Api.Data;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.Outbound;

public class MailboxService
{
    private readonly MarketingStudioDbContext _db;

    public MailboxService(MarketingStudioDbContext db)
    {
        _db = db;
    }

    public async Task<SenderAccount?> GetNextAvailableSenderAsync(Guid tenantId, IEnumerable<Guid> poolIds, CancellationToken cancellationToken = default)
    {
        var idList = poolIds.ToList();

        var senders = await _db.SenderAccounts
            .Where(s => s.TenantId == tenantId && idList.Contains(s.Id) && s.IsActive)
            .ToListAsync(cancellationToken);

        // Find sender with lowest sends today that hasn't exceeded its quota
        return senders
            .Where(s => s.CanSendToday())
            .OrderBy(s => s.SentTodayCount)
            .FirstOrDefault();
    }

    public async Task RecordSendAsync(Guid senderId, CancellationToken cancellationToken = default)
    {
        var sender = await _db.SenderAccounts.FindAsync(new object[] { senderId }, cancellationToken);
        if (sender != null)
        {
            sender.RecordSend();
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
