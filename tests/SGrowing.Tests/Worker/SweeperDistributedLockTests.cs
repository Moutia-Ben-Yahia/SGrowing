using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Common.Concurrency;
using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class SweeperDistributedLockTests
{
    private static (ServiceProvider ServiceProvider, SGrowingDbContext Db, IConcurrencyLockService LockService) CreateContext()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        services.AddSingleton<ITenantContext>(tenant);

        var lockService = new RedisConcurrencyLockService();
        services.AddSingleton<IConcurrencyLockService>(lockService);

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();

        return (sp, db, lockService);
    }

    [Fact]
    public async Task CallCenterLockSweeperDaemon_WhenLockAcquired_CleansExpiredLocks()
    {
        var (sp, db, lockService) = CreateContext();
        var tenantId = Guid.NewGuid();

        var expiredLock = CallCenterOrderLockRecord.Create(
            tenantId,
            Guid.NewGuid(),
            "ORD-1001",
            Guid.NewGuid(),
            "Agent Sarah",
            TimeSpan.FromMinutes(-5) // Expired
        );
        db.CallCenterOrderLocks.Add(expiredLock);
        await db.SaveChangesAsync();

        var daemon = new CallCenterLockSweeperDaemon(
            sp,
            NullLogger<CallCenterLockSweeperDaemon>.Instance,
            null,
            lockService);

        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);
        swept.Should().Be(1);

        var updated = await db.CallCenterOrderLocks.FindAsync(expiredLock.Id);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CartReservationSweeperDaemon_WhenLockAcquired_ReturnsStockToAvailable()
    {
        var (sp, db, lockService) = CreateContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "IPHONE-16-PRO";

        var stock = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            ProductName = "iPhone 16 Pro",
            QuantityAvailable = 7,
            QuantityReserved = 3,
            UnitCost = 850m
        };
        db.WarehouseStockRecords.Add(stock);

        var reservation = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 3,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10) // Expired
        };
        db.CartReservationLocks.Add(reservation);
        await db.SaveChangesAsync();

        var daemon = new CartReservationSweeperDaemon(
            sp,
            NullLogger<CartReservationSweeperDaemon>.Instance,
            null,
            lockService);

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);
        returned.Should().Be(3);

        var updatedStock = await db.WarehouseStockRecords.FindAsync(stock.Id);
        updatedStock!.QuantityReserved.Should().Be(0);
        updatedStock.QuantityAvailable.Should().Be(10);
    }
}
