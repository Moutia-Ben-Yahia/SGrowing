using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CourierDeliverySyncTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task SyncBatchAsync_WithValidDeliveries_SavesRecordsAndReconcilesCash()
    {
        using var db = CreateDbContext();
        var service = new CourierDeliverySyncService(db);
        var tenantId = Guid.NewGuid();
        var driverId = "driver_yalidine_01";

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var recordedTime = DateTime.UtcNow.AddMinutes(-30);

        var batch = new CourierDeliverySyncBatchRequest(
            TenantId: tenantId,
            Deliveries: new List<CourierDeliverySyncItemDto>
            {
                new(p1, "YAL-10001", driverId, "16", "Delivered", 4500m, "sig_hash_01", recordedTime),
                new(p2, "YAL-10002", driverId, "16", "Delivered", 3200m, "sig_hash_02", recordedTime.AddMinutes(5))
            }
        );

        var result = await service.SyncBatchAsync(batch);

        result.TotalReceived.Should().Be(2);
        result.SuccessfullySynced.Should().Be(2);
        result.DuplicatesIgnored.Should().Be(0);
        result.TotalCashReconciled.Should().Be(7700m);
        result.SyncedParcelIds.Should().Contain(new[] { p1, p2 });

        var driverRecords = await service.GetDriverDeliveriesAsync(tenantId, driverId);
        driverRecords.Should().HaveCount(2);
        driverRecords.All(d => d.IsOfflineSync).Should().BeTrue();
    }

    [Fact]
    public async Task SyncBatchAsync_WithDuplicateItems_IgnoresDuplicatesIdempotently()
    {
        using var db = CreateDbContext();
        var service = new CourierDeliverySyncService(db);
        var tenantId = Guid.NewGuid();
        var driverId = "driver_procolis_09";

        var p1 = Guid.NewGuid();
        var recordedTime = DateTime.UtcNow.AddHours(-1);

        var firstBatch = new CourierDeliverySyncBatchRequest(
            TenantId: tenantId,
            Deliveries: new List<CourierDeliverySyncItemDto>
            {
                new(p1, "PRC-99001", driverId, "31", "Delivered", 5000m, "sig_01", recordedTime)
            }
        );

        await service.SyncBatchAsync(firstBatch);

        // Send duplicate batch (e.g. PWA reconnected again and re-submitted same local storage queue)
        var secondBatch = new CourierDeliverySyncBatchRequest(
            TenantId: tenantId,
            Deliveries: new List<CourierDeliverySyncItemDto>
            {
                new(p1, "PRC-99001", driverId, "31", "Delivered", 5000m, "sig_01", recordedTime),
                new(Guid.NewGuid(), "PRC-99002", driverId, "31", "FailedAttempt", 0m, null, recordedTime.AddMinutes(10))
            }
        );

        var result = await service.SyncBatchAsync(secondBatch);

        result.TotalReceived.Should().Be(2);
        result.SuccessfullySynced.Should().Be(1);
        result.DuplicatesIgnored.Should().Be(1);
        result.TotalCashReconciled.Should().Be(0m);

        var allRecords = await service.GetDriverDeliveriesAsync(tenantId, driverId);
        allRecords.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncBatchAsync_WithEmptyList_ReturnsZeroSafely()
    {
        using var db = CreateDbContext();
        var service = new CourierDeliverySyncService(db);

        var result = await service.SyncBatchAsync(new CourierDeliverySyncBatchRequest(Guid.NewGuid(), new List<CourierDeliverySyncItemDto>()));

        result.TotalReceived.Should().Be(0);
        result.SuccessfullySynced.Should().Be(0);
        result.TotalCashReconciled.Should().Be(0m);
    }

    [Fact]
    public async Task GetDriverReconciliationSummaryAsync_ComputesAggregatesAccurately()
    {
        using var db = CreateDbContext();
        var service = new CourierDeliverySyncService(db);
        var tenantId = Guid.NewGuid();
        var driverId = "driver_zr_express_44";

        var batch = new CourierDeliverySyncBatchRequest(
            TenantId: tenantId,
            Deliveries: new List<CourierDeliverySyncItemDto>
            {
                new(Guid.NewGuid(), "ZR-01", driverId, "09", "Delivered", 2000m, null, DateTime.UtcNow.AddMinutes(-40)),
                new(Guid.NewGuid(), "ZR-02", driverId, "09", "Delivered", 3000m, null, DateTime.UtcNow.AddMinutes(-30)),
                new(Guid.NewGuid(), "ZR-03", driverId, "09", "FailedAttempt", 0m, null, DateTime.UtcNow.AddMinutes(-20)),
                new(Guid.NewGuid(), "ZR-04", driverId, "09", "Returned", 0m, null, DateTime.UtcNow.AddMinutes(-10))
            }
        );

        await service.SyncBatchAsync(batch);

        var summary = await service.GetDriverReconciliationSummaryAsync(tenantId, driverId);

        summary.CourierDriverId.Should().Be(driverId);
        summary.TotalDeliveries.Should().Be(4);
        summary.SuccessfulDeliveries.Should().Be(2);
        summary.FailedAttempts.Should().Be(2);
        summary.TotalCashCollected.Should().Be(5000m);
    }
}
