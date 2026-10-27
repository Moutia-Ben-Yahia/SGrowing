using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Diagnostics;

namespace SGrowing.Worker.Services;

public class P2PWarmupRescueDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<P2PWarmupRescueDaemon>? _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    private readonly IDaemonHeartbeatService? _heartbeatService;

    public P2PWarmupRescueDaemon(
        IServiceProvider serviceProvider,
        ILogger<P2PWarmupRescueDaemon>? logger = null,
        IDaemonHeartbeatService? heartbeatService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _heartbeatService = heartbeatService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("P2PWarmupRescueDaemon background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("P2PWarmupRescueDaemon", stoppingToken);
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await ProcessWarmupAndRescueAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing P2P warmup exchanges and spam rescues.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> ProcessWarmupAndRescueAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var activeWarmupSenders = await db.SenderAccounts
            .Where(s => s.IsActive && s.WarmupEnabled && s.SentTodayCount < s.DailyQuota)
            .Take(20)
            .ToListAsync(cancellationToken);

        int processed = 0;

        foreach (var sender in activeWarmupSenders)
        {
            sender.RecordSend();

            var peerRecord = new WarmupRecord
            {
                Id = Guid.NewGuid(),
                TenantId = sender.TenantId,
                SenderAccountId = sender.Id,
                PeerEmail = "peer-warmup-pool@verified-agents.org",
                Direction = WarmupDirection.Sent,
                Status = WarmupStatus.Inbox,
                OccurredAtUtc = DateTime.UtcNow
            };
            db.WarmupRecords.Add(peerRecord);

            // Periodically trigger a spam rescue training simulation
            if (Random.Shared.Next(0, 5) == 0)
            {
                var rescueRecord = new WarmupRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = sender.TenantId,
                    SenderAccountId = sender.Id,
                    PeerEmail = "spam-rescue-monitor@verified-agents.org",
                    Direction = WarmupDirection.Received,
                    Status = WarmupStatus.SpamRescued,
                    OccurredAtUtc = DateTime.UtcNow
                };
                db.WarmupRecords.Add(rescueRecord);
            }

            processed++;
        }

        if (processed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }
}
