using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.CartReservations;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class ConcurrentStockAllocationStressTests
{
    [Fact]
    public async Task TryAcquireLockAsync_AcquiresExclusiveLock_AndBlocksConcurrentHolders()
    {
        // Arrange
        var lockService = new DistributedMemoryLockService();
        string resourceKey = $"lock:tenant:{Guid.NewGuid()}:sku:TEST-SKU-99";

        // Act
        await using var handle1 = await lockService.TryAcquireLockAsync(resourceKey, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        var isLocked = await lockService.IsLockedAsync(resourceKey);

        // Second caller attempts to acquire with zero wait timeout
        await using var handle2 = await lockService.TryAcquireLockAsync(resourceKey, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));

        // Assert
        handle1.Should().NotBeNull();
        handle1!.IsAcquired.Should().BeTrue();
        isLocked.Should().BeTrue();
        handle2.Should().BeNull("resource is currently held exclusively by handle1");
    }

    [Fact]
    public async Task ReleaseLockAsync_AllowsSubsequentCallerToAcquire()
    {
        // Arrange
        var lockService = new DistributedMemoryLockService();
        string resourceKey = $"lock:tenant:{Guid.NewGuid()}:sku:TEST-SKU-RELEASE";

        // Act & Assert
        await using (var handle1 = await lockService.TryAcquireLockAsync(resourceKey, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)))
        {
            handle1.Should().NotBeNull();
        } // handle1 disposed here, releasing lock

        var isStillLocked = await lockService.IsLockedAsync(resourceKey);
        isStillLocked.Should().BeFalse();

        await using var handle2 = await lockService.TryAcquireLockAsync(resourceKey, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50));
        handle2.Should().NotBeNull("lock was released and is available for subsequent callers");
    }

    [Fact]
    public async Task ConcurrentStockAllocation_UnderHighContention_PreventsOverselling()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ConcurrentStockAllocDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        const string sku = "FLASH-SALE-SKU";
        const int initialStock = 10;
        const int concurrentOrders = 25;

        using (var dbInit = new SGrowingDbContext(options))
        {
            dbInit.WarehouseStockRecords.Add(new WarehouseStockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = warehouseId,
                Sku = sku,
                QuantityAvailable = initialStock,
                QuantityReserved = 0,
                RowVersion = 1
            });
            await dbInit.SaveChangesAsync();
        }

        // Act: Run 25 parallel reservation requests each trying to reserve 1 unit
        var tasks = Enumerable.Range(1, concurrentOrders).Select(async i =>
        {
            using var db = new SGrowingDbContext(options);
            var cartService = new SGrowing.Api.Modules.Commerce.CartReservations.CartReservationService(db);
            var req = new AcquireCartReservationRequest(
                WarehouseId: warehouseId,
                Sku: sku,
                Quantity: 1,
                CartId: Guid.NewGuid()
            );
            return await cartService.AcquireReservationAsync(tenantId, req);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        int successfulAllocations = results.Count(r => r.Success);
        int failedAllocations = results.Count(r => !r.Success);

        successfulAllocations.Should().Be(initialStock, "only the available 10 units should be successfully allocated");
        failedAllocations.Should().Be(concurrentOrders - initialStock, "the remaining 15 orders must be rejected due to insufficient inventory");

        using var verifyDb = new SGrowingDbContext(options);
        var finalStock = await verifyDb.WarehouseStockRecords.FirstAsync(s => s.Sku == sku);
        finalStock.QuantityAvailable.Should().Be(0);
        finalStock.QuantityReserved.Should().Be(initialStock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task AcquireReservation_ZeroOrNegativeQuantity_ReturnsRejectedResult(int invalidQuantity)
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"InvalidQtyDb_{Guid.NewGuid()}")
            .Options;

        using var db = new SGrowingDbContext(options);
        var cartService = new CartReservationService(db);
        var tenantId = Guid.NewGuid();

        var req = new AcquireCartReservationRequest(
            WarehouseId: Guid.NewGuid(),
            Sku: "TEST-SKU",
            Quantity: invalidQuantity,
            CartId: Guid.NewGuid()
        );

        var result = await cartService.AcquireReservationAsync(tenantId, req);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Rejected");
        result.Message.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task ReleaseReservation_ActiveReservation_RestoresAvailableStock()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReleaseResDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        const string sku = "RELEASE-TEST-SKU";

        using (var dbInit = new SGrowingDbContext(options))
        {
            dbInit.WarehouseStockRecords.Add(new WarehouseStockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = warehouseId,
                Sku = sku,
                QuantityAvailable = 5,
                QuantityReserved = 0
            });
            await dbInit.SaveChangesAsync();
        }

        using var db = new SGrowingDbContext(options);
        var cartService = new CartReservationService(db);

        // 1. Acquire reservation of 2 units
        var acqReq = new AcquireCartReservationRequest(
            WarehouseId: warehouseId,
            Sku: sku,
            Quantity: 2,
            CartId: cartId
        );
        var acqResult = await cartService.AcquireReservationAsync(tenantId, acqReq);
        acqResult.Success.Should().BeTrue();

        // 2. Release reservation
        var relReq = new ReleaseCartReservationRequest(CartId: cartId);
        var released = await cartService.ReleaseReservationAsync(tenantId, relReq);
        released.Should().BeTrue();

        // 3. Verify stock restored
        var stock = await db.WarehouseStockRecords.FirstAsync(s => s.Sku == sku);
        stock.QuantityAvailable.Should().Be(5);
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task CommitReservation_MarksReservationStatusCommitted()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CommitResDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string sku = "COMMIT-TEST-SKU";

        using (var dbInit = new SGrowingDbContext(options))
        {
            dbInit.WarehouseStockRecords.Add(new WarehouseStockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = warehouseId,
                Sku = sku,
                QuantityAvailable = 10,
                QuantityReserved = 0
            });
            await dbInit.SaveChangesAsync();
        }

        using var db = new SGrowingDbContext(options);
        var cartService = new CartReservationService(db);

        var acqReq = new AcquireCartReservationRequest(
            WarehouseId: warehouseId,
            Sku: sku,
            Quantity: 3,
            CartId: cartId
        );
        await cartService.AcquireReservationAsync(tenantId, acqReq);

        var committed = await cartService.CommitReservationAsync(tenantId, cartId, orderId);
        committed.Should().BeTrue();

        var lockRec = await db.CartReservationLocks.FirstAsync(l => l.CartId == cartId);
        lockRec.Status.Should().Be("Committed");
    }

    [Fact]
    public async Task CommitReservation_WhenReservationNotFound_ReturnsFalse()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CommitMissingDb_{Guid.NewGuid()}")
            .Options;

        using var db = new SGrowingDbContext(options);
        var cartService = new CartReservationService(db);
        var tenantId = Guid.NewGuid();

        var committed = await cartService.CommitReservationAsync(tenantId, Guid.NewGuid(), Guid.NewGuid());

        committed.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseReservation_WhenReservationNotFound_ReturnsFalse()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReleaseMissingDb_{Guid.NewGuid()}")
            .Options;

        using var db = new SGrowingDbContext(options);
        var cartService = new CartReservationService(db);
        var tenantId = Guid.NewGuid();

        var released = await cartService.ReleaseReservationAsync(tenantId, new ReleaseCartReservationRequest(CartId: Guid.NewGuid()));

        released.Should().BeFalse();
    }
}