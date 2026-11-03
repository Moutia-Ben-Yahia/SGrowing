using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class CartReservationSweeperDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenNoLocks_ReturnsZero()
    {
        using var db = CreateDbContext();

        var sweptCount = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        sweptCount.Should().Be(0);
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenExpiredLocksExist_RestoresAvailableStockAndExpiresLock()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SKU-PREMIUM-ROASTER-01";

        var stock = new WarehouseStockRecord
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            ProductName = "Specialty Espresso Roaster",
            QuantityAvailable = 10,
            QuantityReserved = 5,
            UnitCost = 450.0m
        };
        db.WarehouseStockRecords.Add(stock);

        var expiredLock = new CartReservationLockRecord
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 3,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5) // Expired 5 minutes ago
        };
        db.CartReservationLocks.Add(expiredLock);
        await db.SaveChangesAsync();

        var returnedStock = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returnedStock.Should().Be(3);

        var updatedStock = await db.WarehouseStockRecords.FirstAsync(s => s.Id == stock.Id);
        updatedStock.QuantityAvailable.Should().Be(13); // 10 + 3
        updatedStock.QuantityReserved.Should().Be(2);  // 5 - 3

        var updatedLock = await db.CartReservationLocks.FirstAsync(l => l.Id == expiredLock.Id);
        updatedLock.Status.Should().Be("Expired");
        updatedLock.ReleasedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenActiveLocksNotExpired_DoesNotTouchStock()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SKU-BEANS-ETHIOPIA-500G";

        var stock = new WarehouseStockRecord
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            ProductName = "Yirgacheffe Coffee Beans",
            QuantityAvailable = 20,
            QuantityReserved = 4
        };
        db.WarehouseStockRecords.Add(stock);

        var activeLock = new CartReservationLockRecord
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 4,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10) // Still valid for 10 minutes
        };
        db.CartReservationLocks.Add(activeLock);
        await db.SaveChangesAsync();

        var returnedStock = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returnedStock.Should().Be(0);

        var updatedStock = await db.WarehouseStockRecords.FirstAsync(s => s.Id == stock.Id);
        updatedStock.QuantityAvailable.Should().Be(20);
        updatedStock.QuantityReserved.Should().Be(4);

        var updatedLock = await db.CartReservationLocks.FirstAsync(l => l.Id == activeLock.Id);
        updatedLock.Status.Should().Be("Active");
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenAlreadyExpiredOrReleased_Ignores()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SKU-GRINDER-BURR";

        var releasedLock = new CartReservationLockRecord
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 2,
            CartId = Guid.NewGuid(),
            Status = "Released",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-20)
        };
        var committedLock = new CartReservationLockRecord
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 2,
            CartId = Guid.NewGuid(),
            Status = "Committed",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-30)
        };
        db.CartReservationLocks.AddRange(releasedLock, committedLock);
        await db.SaveChangesAsync();

        var returnedStock = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returnedStock.Should().Be(0);
    }

    [Fact]
    public async Task SweepExpiredCartReservationsAsync_WhenWarehouseStockMissing_MarksLockExpiredGracefully()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SKU-DISCONTINUED-MUG";

        var expiredLock = new CartReservationLockRecord
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = sku,
            Quantity = 2,
            CartId = Guid.NewGuid(),
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        db.CartReservationLocks.Add(expiredLock);
        await db.SaveChangesAsync();

        var returnedStock = await CartReservationSweeperDaemon.SweepExpiredCartReservationsAsync(db);

        returnedStock.Should().Be(0); // No warehouse stock record to increment

        var updatedLock = await db.CartReservationLocks.FirstAsync(l => l.Id == expiredLock.Id);
        updatedLock.Status.Should().Be("Expired");
        updatedLock.ReleasedAtUtc.Should().NotBeNull();
    }
}
