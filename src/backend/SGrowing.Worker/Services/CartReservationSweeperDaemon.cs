using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class CartReservationSweeperDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CartReservationSweeperDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly IConcurrencyLockService? _lockService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public CartReservationSweeperDaemon(
        IServiceProvider serviceProvider,
        ILogger<CartReservationSweeperDaemon>? logger = null,
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
        _logger?.LogInformation("CartReservationSweeperDaemon background worker started (Flash-sale inventory release engine).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("CartReservationSweeperDaemon", stoppingToken);
                }

                var lockService = _lockService ?? _serviceProvider.GetService<IConcurrencyLockService>();
                if (lockService != null)
                {
                    await using var lockHandle = await lockService.TryAcquireLockAsync(
                        "sweeper:CartReservations",
                        TimeSpan.FromSeconds(25),
                        TimeSpan.FromMilliseconds(200),
                        stoppingToken);

                    if (lockHandle == null || !lockHandle.IsAcquired)
                    {
                        _logger?.LogDebug("CartReservationSweeperDaemon: Another worker instance holds the leader lease. Skipping sweep.");
                        await Task.Delay(_pollInterval, stoppingToken);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                    await SweepExpiredCartReservationsAsync(db, stoppingToken);
                }
                else
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                    await SweepExpiredCartReservationsAsync(db, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error sweeping expired cart reservation locks.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> SweepExpiredCartReservationsAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredLocks = await db.CartReservationLocks
            .Where(l => l.Status == "Active" && l.ExpiresAtUtc < now)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (!expiredLocks.Any()) return 0;

        int stockReturned = 0;
        foreach (var lockRec in expiredLocks)
        {
            var stock = await db.WarehouseStockRecords
                .FirstOrDefaultAsync(s => s.TenantId == lockRec.TenantId && s.WarehouseId == lockRec.WarehouseId && s.Sku == lockRec.Sku, cancellationToken);

            if (stock != null)
            {
                stock.QuantityReserved = Math.Max(0, stock.QuantityReserved - lockRec.Quantity);
                stock.QuantityAvailable += lockRec.Quantity;
                stock.RowVersion++;
                stock.UpdatedAtUtc = DateTime.UtcNow;
                stockReturned += lockRec.Quantity;
            }

            lockRec.Status = "Expired";
            lockRec.ReleasedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return stockReturned;
    }
}
