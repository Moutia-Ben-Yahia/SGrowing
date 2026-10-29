using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class AndroidSmsQueueDispatcherDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AndroidSmsQueueDispatcherDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(15);

    public AndroidSmsQueueDispatcherDaemon(
        IServiceProvider serviceProvider,
        ILogger<AndroidSmsQueueDispatcherDaemon>? logger = null,
        IDaemonHeartbeatService? heartbeatService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _heartbeatService = heartbeatService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("AndroidSmsQueueDispatcherDaemon started (SIM hardware queue pacing).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("AndroidSmsQueueDispatcherDaemon", stoppingToken);
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await DispatchQueuedMessagesAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing queued Android SMS gateway dispatches.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> DispatchQueuedMessagesAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var queuedDispatches = await db.AndroidSmsDispatchRecords
            .Where(r => r.DispatchStatus == "Queued")
            .OrderBy(r => r.SentAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken);

        if (!queuedDispatches.Any()) return 0;

        int dispatchedCount = 0;
        foreach (var dispatch in queuedDispatches)
        {
            var device = await db.AndroidSmsGatewayDevices
                .FirstOrDefaultAsync(d => d.TenantId == dispatch.TenantId && d.IsOnline && d.DailySentCount < d.DailyQuotaLimit, cancellationToken);

            if (device != null)
            {
                dispatch.DeviceId = device.Id;
                dispatch.DispatchStatus = "Dispatched";
                device.DailySentCount++;
                dispatchedCount++;

                SGrowingTelemetry.AndroidSmsDispatchedCounter.Add(1,
                    new KeyValuePair<string, object?>("tenant_id", dispatch.TenantId.ToString()),
                    new KeyValuePair<string, object?>("device_id", device.Id.ToString()));
            }
        }

        if (dispatchedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return dispatchedCount;
    }
}
