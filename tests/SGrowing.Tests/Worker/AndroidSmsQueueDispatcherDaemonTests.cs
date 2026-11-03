using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class AndroidSmsQueueDispatcherDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenNoQueuedMessages_ReturnsZero()
    {
        using var db = CreateDbContext();

        var count = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        count.Should().Be(0);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenOnlineDeviceWithQuota_DispatchesMessagesAndIncrementsCounters()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var device = AndroidSmsGatewayDevice.Create(
            tenantId,
            "Samsung Galaxy S23 Ultra",
            "imei_359876543210123",
            "fcm_token_s23",
            dailyQuota: 100
        );
        device.DailySentCount = 10;

        var dispatchRecord = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientPhone = "+21699887766",
            MessageBody = "Votre commande SGrowing #1042 est en cours d'expedition.",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };

        db.AndroidSmsGatewayDevices.Add(device);
        db.AndroidSmsDispatchRecords.Add(dispatchRecord);
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(1);

        var updatedRecord = await db.AndroidSmsDispatchRecords.FindAsync(dispatchRecord.Id);
        updatedRecord!.DispatchStatus.Should().Be("Dispatched");
        updatedRecord.DeviceId.Should().Be(device.Id);

        var updatedDevice = await db.AndroidSmsGatewayDevices.FindAsync(device.Id);
        updatedDevice!.DailySentCount.Should().Be(11);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenDeviceAtQuotaLimit_SkipsDevice()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var device = AndroidSmsGatewayDevice.Create(
            tenantId,
            "Xiaomi Redmi Note 12",
            "imei_865432109876543",
            "fcm_token_redmi",
            dailyQuota: 50
        );
        device.DailySentCount = 50; // Reached quota

        var dispatchRecord = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientPhone = "+21620112233",
            MessageBody = "Verification code OTP 849201",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };

        db.AndroidSmsGatewayDevices.Add(device);
        db.AndroidSmsDispatchRecords.Add(dispatchRecord);
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(0);

        var record = await db.AndroidSmsDispatchRecords.FindAsync(dispatchRecord.Id);
        record!.DispatchStatus.Should().Be("Queued");
        record.DeviceId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenDeviceOffline_SkipsDevice()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var device = AndroidSmsGatewayDevice.Create(
            tenantId,
            "Pixel 7 Pro",
            "imei_123456789012345",
            "fcm_token_pixel",
            dailyQuota: 100
        );
        device.IsOnline = false; // Offline

        var dispatchRecord = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientPhone = "+21620112233",
            MessageBody = "Order Confirmation",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };

        db.AndroidSmsGatewayDevices.Add(device);
        db.AndroidSmsDispatchRecords.Add(dispatchRecord);
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(0);
    }
}
