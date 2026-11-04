using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class CartReservationSweeperDaemonTests
{
    private static (IServiceProvider Provider, SGrowingDbContext Db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = $"CartSweeperTests_{Guid.NewGuid()}";
        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenNoLocks_ReturnsZero()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returned.Should().Be(0);
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenLocksStillValid_SkipsThem()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var activeLock = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-PREMIUM-01",
            Quantity = 3,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(12)
        };

        db.CartReservationLocks.Add(activeLock);
        await db.SaveChangesAsync();

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returned.Should().Be(0);
        var inDb = await db.CartReservationLocks.FindAsync(activeLock.Id);
        inDb!.Status.Should().Be("Active");
        inDb.ReleasedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenExpired_ReleasesInventoryAndMarksExpired()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SKU-WIRELESS-EARBUDS";

        var stock = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            ProductName = "Wireless Earbuds ANC",
            QuantityAvailable = 15,
            QuantityReserved = 5,
            RowVersion = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.WarehouseStockRecords.Add(stock);

        var expiredLock = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 5,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5) // expired 5 minutes ago
        };
        db.CartReservationLocks.Add(expiredLock);
        await db.SaveChangesAsync();

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returned.Should().Be(5);

        var updatedStock = await db.WarehouseStockRecords.FindAsync(stock.Id);
        updatedStock!.QuantityReserved.Should().Be(0);
        updatedStock.QuantityAvailable.Should().Be(20);
        updatedStock.RowVersion.Should().Be(2);
        updatedStock.UpdatedAtUtc.Should().NotBeNull();

        var updatedLock = await db.CartReservationLocks.FindAsync(expiredLock.Id);
        updatedLock!.Status.Should().Be("Expired");
        updatedLock.ReleasedAtUtc.Should().NotBeNull();
        updatedLock.ReleasedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenStatusNotActive_SkipsSweep()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var committedLock = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-PAID-01",
            Quantity = 2,
            CartId = Guid.NewGuid(),
            Status = "Committed",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        db.CartReservationLocks.Add(committedLock);
        await db.SaveChangesAsync();

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returned.Should().Be(0);
        var inDb = await db.CartReservationLocks.FindAsync(committedLock.Id);
        inDb!.Status.Should().Be("Committed");
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenWarehouseStockMissing_MarksExpiredGracefully()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var orphanLock = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-DISCONTINUED-99",
            Quantity = 4,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-2)
        };

        db.CartReservationLocks.Add(orphanLock);
        await db.SaveChangesAsync();

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returned.Should().Be(0); // 0 stock returned because stock record didn't exist
        var inDb = await db.CartReservationLocks.FindAsync(orphanLock.Id);
        inDb!.Status.Should().Be("Expired");
        inDb.ReleasedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenMultipleExpiredLocks_AggregatesReturnedQuantity()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var stockA = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-A",
            QuantityAvailable = 10,
            QuantityReserved = 8,
            RowVersion = 1
        };
        var stockB = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-B",
            QuantityAvailable = 5,
            QuantityReserved = 4,
            RowVersion = 1
        };
        db.WarehouseStockRecords.AddRange(stockA, stockB);

        var lock1 = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-A",
            Quantity = 3,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        var lock2 = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-A",
            Quantity = 5,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        var lock3 = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-B",
            Quantity = 4,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };

        db.CartReservationLocks.AddRange(lock1, lock2, lock3);
        await db.SaveChangesAsync();

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returned.Should().Be(12); // 3 + 5 + 4 = 12

        var stockAInDb = await db.WarehouseStockRecords.FindAsync(stockA.Id);
        stockAInDb!.QuantityAvailable.Should().Be(18);
        stockAInDb.QuantityReserved.Should().Be(0);

        var stockBInDb = await db.WarehouseStockRecords.FindAsync(stockB.Id);
        stockBInDb!.QuantityAvailable.Should().Be(9);
        stockBInDb.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenMoreThan100Expired_LimitsBatchTo100()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        for (int i = 0; i < 110; i++)
        {
            var lockRec = new CartReservationLockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = warehouseId,
                Sku = $"SKU-BATCH-{i}",
                Quantity = 1,
                CartId = Guid.NewGuid(),
                Status = "Active",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };
            db.CartReservationLocks.Add(lockRec);
        }
        await db.SaveChangesAsync();

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        // Swept 100 out of 110 (all stock records missing, so returned = 0, but 100 marked Expired)
        var expiredCount = await db.CartReservationLocks.CountAsync(l => l.Status == "Expired");
        var activeCount = await db.CartReservationLocks.CountAsync(l => l.Status == "Active");

        expiredCount.Should().Be(100);
        activeCount.Should().Be(10);
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenReservedLessThenLockQuantity_ClampsReservedToZero()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SKU-CLAMP-TEST";

        var stock = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            ProductName = "Clamping Test Item",
            QuantityAvailable = 10,
            QuantityReserved = 2, // Less than lock quantity
            RowVersion = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.WarehouseStockRecords.Add(stock);

        var lockRec = new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 5,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-3)
        };
        db.CartReservationLocks.Add(lockRec);
        await db.SaveChangesAsync();

        var returned = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returned.Should().Be(5);
        var updatedStock = await db.WarehouseStockRecords.FindAsync(stock.Id);
        updatedStock!.QuantityReserved.Should().Be(0); // Clamped from 2 - 5 to 0
        updatedStock.QuantityAvailable.Should().Be(15);

        var updatedLock = await db.CartReservationLocks.FindAsync(lockRec.Id);
        updatedLock!.Status.Should().Be("Expired");
    }
}
