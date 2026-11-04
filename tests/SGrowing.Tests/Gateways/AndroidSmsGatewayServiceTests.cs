using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Gateways.Android;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Gateways;

public class AndroidSmsGatewayServiceTests
{
    private static SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AndroidSmsDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task RegisterDeviceAsync_CreatesNewDeviceAndUpdatesExistingIdempotently()
    {
        using var db = CreateDbContext();
        var service = new AndroidSmsGatewayService(db);
        var tenantId = Guid.NewGuid();

        // Register new device
        var device = await service.RegisterDeviceAsync(
            tenantId,
            "Pixel 9 Pro Gateway",
            "IMEI-998877665544332",
            "fcm_token_initial",
            sim1: "Ooredoo TN",
            sim2: "Orange TN",
            dailyQuota: 600
        );

        device.Should().NotBeNull();
        device.DeviceName.Should().Be("Pixel 9 Pro Gateway");
        device.DeviceIdentifier.Should().Be("IMEI-998877665544332");
        device.Sim1Operator.Should().Be("Ooredoo TN");
        device.Sim2Operator.Should().Be("Orange TN");
        device.DailyQuotaLimit.Should().Be(600);
        device.IsOnline.Should().BeTrue();

        // Re-register with updated tokens and quota
        var updated = await service.RegisterDeviceAsync(
            tenantId,
            "Pixel 9 Pro Gateway (Updated)",
            "IMEI-998877665544332",
            "fcm_token_renewed",
            sim1: "Ooredoo Business",
            sim2: "Telecom TN",
            dailyQuota: 800
        );

        updated.Id.Should().Be(device.Id);
        updated.DeviceName.Should().Be("Pixel 9 Pro Gateway (Updated)");
        updated.FcmToken.Should().Be("fcm_token_renewed");
        updated.DailyQuotaLimit.Should().Be(800);

        var count = await db.AndroidSmsGatewayDevices.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task RecordHeartbeatAsync_UpdatesBatteryAndLastHeartbeat()
    {
        using var db = CreateDbContext();
        var service = new AndroidSmsGatewayService(db);
        var tenantId = Guid.NewGuid();

        var device = await service.RegisterDeviceAsync(
            tenantId,
            "Samsung Galaxy A54",
            "DEV-SAMSUNG-001",
            "fcm_samsung"
        );

        // Act
        var success = await service.RecordHeartbeatAsync(tenantId, "DEV-SAMSUNG-001", batteryPercent: 88);

        // Assert
        success.Should().BeTrue();
        var inDb = await db.AndroidSmsGatewayDevices.FindAsync(device.Id);
        inDb.Should().NotBeNull();
        inDb!.BatteryPercent.Should().Be(88);
        inDb.IsOnline.Should().BeTrue();

        // Non-existent device returns false
        var failed = await service.RecordHeartbeatAsync(tenantId, "NON-EXISTENT-DEVICE", 50);
        failed.Should().BeFalse();
    }

    [Fact]
    public async Task DispatchSmsAsync_WithAvailableDevice_DispatchesSmsAndIncrementsQuota()
    {
        using var db = CreateDbContext();
        var service = new AndroidSmsGatewayService(db);
        var tenantId = Guid.NewGuid();

        var device = await service.RegisterDeviceAsync(
            tenantId,
            "Xiaomi Gateway 1",
            "DEV-XIAOMI-1",
            "fcm_xiaomi",
            dailyQuota: 100
        );

        // Act
        var result = await service.DispatchSmsAsync(
            tenantId,
            "+21620000111",
            "Votre commande #9810 a été expédiée avec succès !",
            preferredSimSlot: 2
        );

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be("Dispatched");
        result.SimSlot.Should().Be(2);
        result.RecipientPhone.Should().Be("+21620000111");

        var updatedDevice = await db.AndroidSmsGatewayDevices.FindAsync(device.Id);
        updatedDevice!.DailySentCount.Should().Be(1);

        var dispatchInDb = await db.AndroidSmsDispatchRecords.FindAsync(result.DispatchId);
        dispatchInDb.Should().NotBeNull();
        dispatchInDb!.RecipientPhone.Should().Be("+21620000111");
        dispatchInDb.MessageBody.Should().Contain("commande #9810");
    }

    [Fact]
    public async Task DispatchSmsAsync_NoOnlineDeviceOrQuotaExceeded_ReturnsFailureResult()
    {
        using var db = CreateDbContext();
        var service = new AndroidSmsGatewayService(db);
        var tenantId = Guid.NewGuid();

        // No devices registered
        var resultNoDevices = await service.DispatchSmsAsync(tenantId, "+21699999999", "Test failure");
        resultNoDevices.IsSuccess.Should().BeFalse();
        resultNoDevices.Status.Should().Be("Failed");
        resultNoDevices.Message.Should().Contain("No active online Android gateway handset available");

        // Device with full quota
        var maxedDevice = await service.RegisterDeviceAsync(tenantId, "Maxed Phone", "DEV-MAX", "fcm_max", dailyQuota: 5);
        maxedDevice.DailySentCount = 5;
        await db.SaveChangesAsync();

        var resultQuotaExceeded = await service.DispatchSmsAsync(tenantId, "+21699999999", "Test quota breach");
        resultQuotaExceeded.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ListDevicesAndDispatchHistory_ReturnsTenantScopedRecordsOrdered()
    {
        using var db = CreateDbContext();
        var service = new AndroidSmsGatewayService(db);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        var dev1 = await service.RegisterDeviceAsync(tenantId, "Device A", "ID-A", "fcm_a");
        var dev2 = await service.RegisterDeviceAsync(tenantId, "Device B", "ID-B", "fcm_b");
        var devOther = await service.RegisterDeviceAsync(otherTenantId, "Device Other", "ID-OTHER", "fcm_other");

        // Act
        var devices = await service.ListDevicesAsync(tenantId);

        // Assert
        devices.Should().HaveCount(2);
        devices.Should().OnlyContain(d => d.TenantId == tenantId);

        // Dispatch an SMS and verify history
        await service.DispatchSmsAsync(tenantId, "+21611111111", "Message 1");
        var history = await service.GetDispatchHistoryAsync(tenantId, limit: 10);
        history.Should().HaveCount(1);
        history.First().RecipientPhone.Should().Be("+21611111111");
    }

    [Fact]
    public async Task RecordDeliveryStatusAsync_UpdatesStatusAndSetsDeliveredTimestamp()
    {
        using var db = CreateDbContext();
        var service = new AndroidSmsGatewayService(db);
        var tenantId = Guid.NewGuid();

        await service.RegisterDeviceAsync(tenantId, "Gateway Dev", "ID-DEV", "fcm");
        var dispatch = await service.DispatchSmsAsync(tenantId, "+21622222222", "Test status transition");

        // Act
        var updated = await service.RecordDeliveryStatusAsync(tenantId, dispatch.DispatchId!.Value, "Delivered");

        // Assert
        updated.Should().BeTrue();
        var inDb = await db.AndroidSmsDispatchRecords.FindAsync(dispatch.DispatchId.Value);
        inDb.Should().NotBeNull();
        inDb!.DispatchStatus.Should().Be("Delivered");
        inDb.DeliveredAtUtc.Should().NotBeNull();

        // Missing record returns false
        var missingUpdate = await service.RecordDeliveryStatusAsync(tenantId, Guid.NewGuid(), "Failed", "Network timeout");
        missingUpdate.Should().BeFalse();
    }
}
