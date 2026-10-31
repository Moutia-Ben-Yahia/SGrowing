using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class CallCenterLockSweeperDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CallCenterLockSweeperDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly IConcurrencyLockService? _lockService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public CallCenterLockSweeperDaemon(
        IServiceProvider serviceProvider,
        ILogger<CallCenterLockSweeperDaemon>? logger = null,
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
        _logger?.LogInformation("CallCenterLockSweeperDaemon started (Anti-collision sliding TTL lock cleanup).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("CallCenterLockSweeperDaemon", stoppingToken);
                }

                var lockService = _lockService ?? _serviceProvider.GetService<IConcurrencyLockService>();
                if (lockService != null)
                {
                    await using var lockHandle = await lockService.TryAcquireLockAsync(
                        "sweeper:CallCenterLocks",
                        TimeSpan.FromSeconds(25),
                        TimeSpan.FromMilliseconds(200),
                        stoppingToken);

                    if (lockHandle == null || !lockHandle.IsAcquired)
                    {
                        _logger?.LogDebug("CallCenterLockSweeperDaemon: Another worker instance holds the leader lease. Skipping sweep.");
                        await Task.Delay(_pollInterval, stoppingToken);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                    await SweepExpiredLocksAsync(db, stoppingToken);
                }
                else
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                    await SweepExpiredLocksAsync(db, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error sweeping expired call center anti-collision locks.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> SweepExpiredLocksAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredLocks = await db.CallCenterOrderLocks
            .Where(l => l.IsActive && l.ExpiresAtUtc < now)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (!expiredLocks.Any()) return 0;

        foreach (var lockRec in expiredLocks)
        {
            lockRec.Release();
        }

        await db.SaveChangesAsync(cancellationToken);
        return expiredLocks.Count;
    }
}
