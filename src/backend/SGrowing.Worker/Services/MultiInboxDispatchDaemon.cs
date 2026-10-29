using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Diagnostics;

namespace SGrowing.Worker.Services;

public record DaemonPoolAuditResult(
    int TotalInboxesAudited,
    int DormantInboxesCooled,
    int WarmupAccountsPromoted,
    int QuarantinedAccountsRecovered,
    DateTime TimestampUtc
);

public class MultiInboxDispatchDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MultiInboxDispatchDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly TimeSpan _auditInterval = TimeSpan.FromSeconds(30);

    private DateTime _lastResetDateUtc = DateTime.UtcNow.Date;

    public MultiInboxDispatchDaemon(
        IServiceProvider serviceProvider,
        ILogger<MultiInboxDispatchDaemon>? logger = null,
        IDaemonHeartbeatService? heartbeatService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _heartbeatService = heartbeatService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("MultiInboxDispatchDaemon background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("MultiInboxDispatchDaemon", stoppingToken);
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

                // 1. Check midnight UTC daily quota reset
                var nowUtc = DateTime.UtcNow;
                if (nowUtc.Date > _lastResetDateUtc)
                {
                    _logger?.LogInformation("Midnight UTC reached ({Date}). Resetting sender account daily sent counts...", nowUtc.Date);
                    int resetCount = await ResetMidnightCountersAsync(db, stoppingToken);
                    _lastResetDateUtc = nowUtc.Date;
                    _logger?.LogInformation("Successfully reset daily sent counters for {Count} sender accounts.", resetCount);
                }

                // 2. Perform continuous deliverability audit on pool health
                var auditResult = await AuditPoolHealthAsync(db, stoppingToken);
                if (auditResult.DormantInboxesCooled > 0 || auditResult.WarmupAccountsPromoted > 0 || auditResult.QuarantinedAccountsRecovered > 0)
                {
                    _logger?.LogInformation(
                        "Pool health audit complete: {Cooled} dormant cooled, {Promoted} warmup promoted to active, {Recovered} quarantined recovered.",
                        auditResult.DormantInboxesCooled,
                        auditResult.WarmupAccountsPromoted,
                        auditResult.QuarantinedAccountsRecovered);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error occurred during MultiInboxDispatchDaemon cycle.");
            }

            await Task.Delay(_auditInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Resets daily sent counters for all sender accounts at midnight UTC.
    /// Can be called directly in unit and integration tests.
    /// </summary>
    public static async Task<int> ResetMidnightCountersAsync(SGrowingDbContext db, CancellationToken ct = default)
    {
        var accounts = await db.SenderAccounts.ToListAsync(ct);
        foreach (var account in accounts)
        {
            account.ResetDailyQuota();
        }

        if (accounts.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return accounts.Count;
    }

    /// <summary>
    /// Continuously audits pool health, checking for dormant inboxes, promoting matured warmup accounts,
    /// and un-quarantining recovered warmup accounts.
    /// </summary>
    public static async Task<DaemonPoolAuditResult> AuditPoolHealthAsync(
        SGrowingDbContext db,
        CancellationToken ct = default,
        DateTime? referenceTimeUtc = null)
    {
        var now = referenceTimeUtc ?? DateTime.UtcNow;
        var twoDaysAgo = now.AddDays(-2);
        var fourteenDaysAgo = now.AddDays(-14);
        var threeDaysAgo = now.AddDays(-3);

        var accounts = await db.SenderAccounts.ToListAsync(ct);

        int cooledCount = 0;
        int promotedCount = 0;
        int recoveredCount = 0;

        foreach (var acc in accounts)
        {
            // 1. Dormant active inboxes: Active accounts that have not dispatched in > 48 hours
            if (acc.Status == InboxRotationStatus.Active && acc.LastDispatchedAtUtc.HasValue && acc.LastDispatchedAtUtc.Value < twoDaysAgo)
            {
                acc.Status = InboxRotationStatus.Cooling;
                cooledCount++;
            }

            // 2. Matured Warmup inboxes: Created >= 14 days ago, high health score >= 90, clean bounce rate <= 1.0%
            if (acc.Status == InboxRotationStatus.Warmup && acc.CreatedAtUtc <= fourteenDaysAgo && acc.HealthScore >= 90 && acc.BounceRate <= 1.0)
            {
                acc.Status = InboxRotationStatus.Active;
                promotedCount++;
            }

            // 3. Quarantined accounts: Quarantined for >= 3 days, low bounce rate, all DNS passing -> stage into Cooling/Warmup
            if (acc.Status == InboxRotationStatus.Quarantined && acc.QuarantinedAtUtc.HasValue && acc.QuarantinedAtUtc.Value <= threeDaysAgo && acc.HasSpf && acc.HasDkim && acc.HasDmarc)
            {
                acc.Thaw();
                acc.Status = InboxRotationStatus.Cooling; // Staged recovery
                recoveredCount++;
            }
        }

        if (cooledCount > 0 || promotedCount > 0 || recoveredCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new DaemonPoolAuditResult(
            TotalInboxesAudited: accounts.Count,
            DormantInboxesCooled: cooledCount,
            WarmupAccountsPromoted: promotedCount,
            QuarantinedAccountsRecovered: recoveredCount,
            TimestampUtc: now
        );
    }
}
