using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class AndroidSmsQueueDispatcherDaemonTests
{
    private static (IServiceProvider Provider, SGrowingDbContext Db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = $"AndroidSmsDispatcherTests_{Guid.NewGuid()}";
        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenNoQueuedMessages_ReturnsZero()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(0);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenNoOnlineDevice_LeavesMessagesQueued()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        // Offline device
        var device = AndroidSmsGatewayDevice.Create(tenantId, "Redmi Note 12", "IMEI-829104", "fcm-tok");
        device.IsOnline = false;
        db.AndroidSmsGatewayDevices.Add(device);

        var queuedMsg = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientPhone = "+21698123456",
            MessageBody = "Votre commande COD #101 est en cours d'expedition.",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };
        db.AndroidSmsDispatchRecords.Add(queuedMsg);
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(0);
        var inDb = await db.AndroidSmsDispatchRecords.FindAsync(queuedMsg.Id);
        inDb!.DispatchStatus.Should().Be("Queued");
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenDeviceQuotaExhausted_SkipsDispatch()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var device = AndroidSmsGatewayDevice.Create(tenantId, "Samsung A54", "IMEI-992211", "fcm-tok", dailyQuota: 100);
        device.DailySentCount = 100; // Quota full
        db.AndroidSmsGatewayDevices.Add(device);

        var queuedMsg = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientPhone = "+21622334455",
            MessageBody = "Verification OTP: 829104",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };
        db.AndroidSmsDispatchRecords.Add(queuedMsg);
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(0);
        var inDb = await db.AndroidSmsDispatchRecords.FindAsync(queuedMsg.Id);
        inDb!.DispatchStatus.Should().Be("Queued");
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenDeviceOnlineWithQuota_DispatchesAndIncrementsCount()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var device = AndroidSmsGatewayDevice.Create(tenantId, "Pixel 8 Pro Hub", "IMEI-774411", "fcm-tok", dailyQuota: 500);
        device.DailySentCount = 20;
        db.AndroidSmsGatewayDevices.Add(device);

        var queuedMsg = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientPhone = "+21655112233",
            MessageBody = "Votre livreur Yalidine arrivera entre 14h et 16h.",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };
        db.AndroidSmsDispatchRecords.Add(queuedMsg);
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(1);

        var inDb = await db.AndroidSmsDispatchRecords.FindAsync(queuedMsg.Id);
        inDb!.DispatchStatus.Should().Be("Dispatched");
        inDb.DeviceId.Should().Be(device.Id);

        var deviceInDb = await db.AndroidSmsGatewayDevices.FindAsync(device.Id);
        deviceInDb!.DailySentCount.Should().Be(21);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_WhenMoreThan25Queued_LimitsBatchTo25()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var device = AndroidSmsGatewayDevice.Create(tenantId, "Gateway Cluster #1", "IMEI-POOL-1", "fcm-tok", dailyQuota: 1000);
        db.AndroidSmsGatewayDevices.Add(device);

        for (int i = 0; i < 35; i++)
        {
            db.AndroidSmsDispatchRecords.Add(new AndroidSmsDispatchRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RecipientPhone = $"+216981100{i:D2}",
                MessageBody = $"Bulk notification #{i}",
                DispatchStatus = "Queued",
                SentAtUtc = DateTime.UtcNow.AddSeconds(i)
            });
        }
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(25);
        var remainingQueued = await db.AndroidSmsDispatchRecords.CountAsync(r => r.DispatchStatus == "Queued");
        remainingQueued.Should().Be(10);
    }

    [Fact]
    public async Task DispatchQueuedMessagesAsync_EnsuresStrictTenantIsolation()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Device belongs to Tenant B
        var deviceTenantB = AndroidSmsGatewayDevice.Create(tenantB, "Gateway B", "IMEI-B", "fcm-tok", dailyQuota: 500);
        db.AndroidSmsGatewayDevices.Add(deviceTenantB);

        // Message belongs to Tenant A
        var msgTenantA = new AndroidSmsDispatchRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            RecipientPhone = "+21699001122",
            MessageBody = "Tenant A message",
            DispatchStatus = "Queued",
            SentAtUtc = DateTime.UtcNow
        };
        db.AndroidSmsDispatchRecords.Add(msgTenantA);
        await db.SaveChangesAsync();

        var dispatched = await AndroidSmsQueueDispatcherDaemon.DispatchQueuedMessagesAsync(db);

        dispatched.Should().Be(0); // Tenant A has no device registered
        var inDb = await db.AndroidSmsDispatchRecords.FindAsync(msgTenantA.Id);
        inDb!.DispatchStatus.Should().Be("Queued");
    }
}
