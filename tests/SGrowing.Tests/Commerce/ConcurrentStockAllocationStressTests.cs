using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.Warehouses;
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
            dbInit.WarehouseRecords.Add(new WarehouseRecord
            {
                Id = warehouseId,
                TenantId = tenantId,
                Code = "TUN-CENTRAL",
                Name = "Tunis Central Hub",
                Address = "Zone Industrielle Charguia II",
                City = "Tunis",
                Country = "Tunisia",
                Latitude = 36.85,
                Longitude = 10.20,
                IsActive = true
            });

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

        var lockService = new DistributedMemoryLockService();

        // Act: Run 25 parallel order routing requests each trying to order 1 unit
        var tasks = Enumerable.Range(1, concurrentOrders).Select(async i =>
        {
            using var db = new SGrowingDbContext(options);
            var routingService = new WarehouseRoutingService(db, lockService);

            var req = new RoutingRequest(
                TenantId: tenantId,
                OrderId: $"ORD-STRESS-{i}",
                CustomerAddress: "Avenue Habib Bourguiba",
                CustomerCity: "Tunis",
                CustomerLatitude: 36.84,
                CustomerLongitude: 10.19,
                Items: new List<OrderItemRequirement> { new(sku, 1) }
            );

            return await routingService.RouteOrderAsync(req);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        int successfulAllocations = results.Count(r => r.IsAllocated);
        int failedAllocations = results.Count(r => !r.IsAllocated);

        successfulAllocations.Should().Be(initialStock, "only the available 10 units should be successfully allocated");
        failedAllocations.Should().Be(concurrentOrders - initialStock, "the remaining 15 orders must be rejected due to insufficient inventory");

        using (var dbCheck = new SGrowingDbContext(options))
        {
            var stockRecord = await dbCheck.WarehouseStockRecords
                .FirstAsync(s => s.TenantId == tenantId && s.WarehouseId == warehouseId && s.Sku == sku);

            stockRecord.QuantityAvailable.Should().Be(0, "available stock should not drop below zero");
            stockRecord.QuantityReserved.Should().Be(initialStock, "all initial stock should now be safely reserved");
        }
    }
}
