using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class CallCenterLockSweeperDaemonTests
{
    private static (IServiceProvider Provider, SGrowingDbContext Db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = $"CallCenterSweeperTests_{Guid.NewGuid()}";
        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenNoLocks_ReturnsZero()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;

        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        swept.Should().Be(0);
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenLocksStillValid_SkipsSweep()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var validLock = CallCenterOrderLockRecord.Create(
            tenantId,
            orderId,
            "ORD-TUN-901",
            Guid.NewGuid(),
            "Agent Sarah",
            TimeSpan.FromMinutes(5));

        db.CallCenterOrderLocks.Add(validLock);
        await db.SaveChangesAsync();

        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        swept.Should().Be(0);
        var inDb = await db.CallCenterOrderLocks.FindAsync(validLock.Id);
        inDb!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenExpired_ReleasesLockAndMarksInactive()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var expiredLock = CallCenterOrderLockRecord.Create(
            tenantId,
            orderId,
            "ORD-ALG-802",
            Guid.NewGuid(),
            "Agent Youssef",
            TimeSpan.FromMinutes(-2)); // Expired 2 minutes ago

        db.CallCenterOrderLocks.Add(expiredLock);
        await db.SaveChangesAsync();

        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        swept.Should().Be(1);
        var inDb = await db.CallCenterOrderLocks.FindAsync(expiredLock.Id);
        inDb!.IsActive.Should().BeFalse();
        inDb.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenAlreadyInactive_SkipsThem()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var releasedLock = CallCenterOrderLockRecord.Create(
            tenantId,
            orderId,
            "ORD-MAR-703",
            Guid.NewGuid(),
            "Agent Amina",
            TimeSpan.FromMinutes(-10));
        releasedLock.Release();

        db.CallCenterOrderLocks.Add(releasedLock);
        await db.SaveChangesAsync();

        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        swept.Should().Be(0);
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenMoreThan100Expired_LimitsBatchTo100()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        for (int i = 0; i < 120; i++)
        {
            var lockRec = CallCenterOrderLockRecord.Create(
                tenantId,
                Guid.NewGuid(),
                $"ORD-BATCH-{i}",
                Guid.NewGuid(),
                $"Agent {i}",
                TimeSpan.FromMinutes(-5));
            db.CallCenterOrderLocks.Add(lockRec);
        }
        await db.SaveChangesAsync();

        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        swept.Should().Be(100);
        var remainingActive = await db.CallCenterOrderLocks.CountAsync(l => l.IsActive);
        remainingActive.Should().Be(20);
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_AcrossMultipleTenants_SweepsAllExpiredLocks()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;

        var lockTenant1 = CallCenterOrderLockRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ORD-T1-1",
            Guid.NewGuid(),
            "Agent 1",
            TimeSpan.FromMinutes(-1));

        var lockTenant2 = CallCenterOrderLockRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ORD-T2-1",
            Guid.NewGuid(),
            "Agent 2",
            TimeSpan.FromMinutes(-3));

        db.CallCenterOrderLocks.AddRange(lockTenant1, lockTenant2);
        await db.SaveChangesAsync();

        var swept = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        swept.Should().Be(2);
        var allInactive = await db.CallCenterOrderLocks.AllAsync(l => !l.IsActive);
        allInactive.Should().BeTrue();
    }
}
