using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class SerpRankScheduledTrackerDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SerpRankScheduledTrackerDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly IConcurrencyLockService? _lockService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(10);

    public SerpRankScheduledTrackerDaemon(
        IServiceProvider serviceProvider,
        ILogger<SerpRankScheduledTrackerDaemon>? logger = null,
        IDaemonHeartbeatService? heartbeatService = null,
        IConcurrencyLockService? lockService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _heartbeatService = heartbeatService;
        _lockService = lockService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("SerpRankScheduledTrackerDaemon started (Daily Google keyword rank tracking).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("SerpRankScheduledTrackerDaemon", stoppingToken);
                }

                var lockService = _lockService ?? _serviceProvider.GetService<IConcurrencyLockService>();
                if (lockService != null)
                {
                    await using var lockHandle = await lockService.TryAcquireLockAsync(
                        "sweeper:SerpRankScheduledTracker",
                        TimeSpan.FromMinutes(8),
                        TimeSpan.FromMilliseconds(200),
                        stoppingToken);

                    if (lockHandle == null || !lockHandle.IsAcquired)
                    {
                        _logger?.LogDebug("SerpRankScheduledTrackerDaemon: Another worker instance holds the leader lease. Skipping run.");
                        await Task.Delay(_pollInterval, stoppingToken);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                    await AuditTrackedKeywordsAsync(db, stoppingToken);
                }
                else
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                    await AuditTrackedKeywordsAsync(db, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error executing scheduled SERP keyword rank tracking.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> AuditTrackedKeywordsAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var recentKeywords = await db.SerpRankHistoryRecords
            .Where(r => r.RecordedDateUtc > cutoff)
            .Select(r => new { r.TenantId, r.Keyword, r.TargetUrl })
            .ToListAsync(cancellationToken);

        return recentKeywords.Count;
    }
}
