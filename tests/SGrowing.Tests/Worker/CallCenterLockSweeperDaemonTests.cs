using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class CallCenterLockSweeperDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenNoLocks_ReturnsZero()
    {
        using var db = CreateDbContext();

        var sweptCount = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        sweptCount.Should().Be(0);
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenExpiredActiveLocksExist_ReleasesLocksAndReturnsCount()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var expiredLock1 = new CallCenterOrderLockRecord
        {
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-CC-101",
            LockedByUserId = Guid.NewGuid(),
            LockedByUserName = "Agent Selma",
            LockedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5),
            IsActive = true
        };

        var expiredLock2 = new CallCenterOrderLockRecord
        {
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-CC-102",
            LockedByUserId = Guid.NewGuid(),
            LockedByUserName = "Agent Tarek",
            LockedAtUtc = DateTime.UtcNow.AddMinutes(-8),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-3),
            IsActive = true
        };

        db.CallCenterOrderLocks.AddRange(expiredLock1, expiredLock2);
        await db.SaveChangesAsync();

        var sweptCount = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        sweptCount.Should().Be(2);

        var lock1 = await db.CallCenterOrderLocks.FirstAsync(l => l.Id == expiredLock1.Id);
        lock1.IsActive.Should().BeFalse();

        var lock2 = await db.CallCenterOrderLocks.FirstAsync(l => l.Id == expiredLock2.Id);
        lock2.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenLocksStillValid_LeavesActive()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var activeLock = new CallCenterOrderLockRecord
        {
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-CC-103",
            LockedByUserId = Guid.NewGuid(),
            LockedByUserName = "Agent Yasmine",
            LockedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5), // 5 minutes remaining
            IsActive = true
        };

        db.CallCenterOrderLocks.Add(activeLock);
        await db.SaveChangesAsync();

        var sweptCount = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        sweptCount.Should().Be(0);

        var lockRec = await db.CallCenterOrderLocks.FirstAsync(l => l.Id == activeLock.Id);
        lockRec.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SweepExpiredLocksAsync_WhenLockAlreadyInactive_Ignores()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var inactiveLock = new CallCenterOrderLockRecord
        {
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-CC-104",
            LockedByUserId = Guid.NewGuid(),
            LockedByUserName = "Agent Moez",
            LockedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-15),
            IsActive = false // Already released
        };

        db.CallCenterOrderLocks.Add(inactiveLock);
        await db.SaveChangesAsync();

        var sweptCount = await CallCenterLockSweeperDaemon.SweepExpiredLocksAsync(db);

        sweptCount.Should().Be(0);
    }
}
