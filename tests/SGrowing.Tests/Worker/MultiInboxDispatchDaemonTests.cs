using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class MultiInboxDispatchDaemonTests
{
    private static (IServiceProvider Provider, SGrowingDbContext Db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = $"MultiInboxDaemonTests_{Guid.NewGuid()}";
        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Fact]
    public async Task ResetMidnightCountersAsync_WhenAccountsExist_ResetsDailyCounters()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var acc1 = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "outreach1@sgrowing.io",
            DisplayName = "Outreach One",
            SentTodayCount = 35,
            DailyQuota = 50
        };

        var acc2 = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "outreach2@sgrowing.io",
            DisplayName = "Outreach Two",
            SentTodayCount = 18,
            DailyQuota = 40
        };

        db.SenderAccounts.AddRange(acc1, acc2);
        await db.SaveChangesAsync();

        var resetCount = await MultiInboxDispatchDaemon.ResetMidnightCountersAsync(db);

        resetCount.Should().Be(2);

        var updatedAcc1 = await db.SenderAccounts.FirstAsync(a => a.Id == acc1.Id);
        var updatedAcc2 = await db.SenderAccounts.FirstAsync(a => a.Id == acc2.Id);

        updatedAcc1.SentTodayCount.Should().Be(0);
        updatedAcc2.SentTodayCount.Should().Be(0);
    }

    [Fact]
    public async Task AuditPoolHealthAsync_WhenActiveAccountDormantFor48Hours_TransitionsToCooling()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var dormantAcc = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "dormant@sgrowing.io",
            Status = InboxRotationStatus.Active,
            LastDispatchedAtUtc = now.AddDays(-3), // > 48h ago
            CreatedAtUtc = now.AddMonths(-1)
        };

        db.SenderAccounts.Add(dormantAcc);
        await db.SaveChangesAsync();

        var result = await MultiInboxDispatchDaemon.AuditPoolHealthAsync(db, referenceTimeUtc: now);

        result.DormantInboxesCooled.Should().Be(1);
        result.TotalInboxesAudited.Should().Be(1);

        var updated = await db.SenderAccounts.FirstAsync(a => a.Id == dormantAcc.Id);
        updated.Status.Should().Be(InboxRotationStatus.Cooling);
    }

    [Fact]
    public async Task AuditPoolHealthAsync_WhenWarmupAccountMaturedWithHighHealth_PromotesToActive()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var maturedAcc = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "mature-warmup@sgrowing.io",
            Status = InboxRotationStatus.Warmup,
            CreatedAtUtc = now.AddDays(-16), // >= 14 days ago
            HealthScore = 95,               // >= 90
            TotalDispatched = 150,
            TotalDelivered = 150,
            TotalBounced = 0                 // BounceRate 0.0 <= 1.0%
        };

        db.SenderAccounts.Add(maturedAcc);
        await db.SaveChangesAsync();

        var result = await MultiInboxDispatchDaemon.AuditPoolHealthAsync(db, referenceTimeUtc: now);

        result.WarmupAccountsPromoted.Should().Be(1);

        var updated = await db.SenderAccounts.FirstAsync(a => a.Id == maturedAcc.Id);
        updated.Status.Should().Be(InboxRotationStatus.Active);
    }

    [Fact]
    public async Task AuditPoolHealthAsync_WhenQuarantinedAccountMaturedWithValidDns_ThawsAndTransitionsToCooling()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var quarantinedAcc = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "quarantined@sgrowing.io",
            Status = InboxRotationStatus.Quarantined,
            QuarantinedAtUtc = now.AddDays(-4), // >= 3 days ago
            HasSpf = true,
            HasDkim = true,
            HasDmarc = true
        };

        db.SenderAccounts.Add(quarantinedAcc);
        await db.SaveChangesAsync();

        var result = await MultiInboxDispatchDaemon.AuditPoolHealthAsync(db, referenceTimeUtc: now);

        result.QuarantinedAccountsRecovered.Should().Be(1);

        var updated = await db.SenderAccounts.FirstAsync(a => a.Id == quarantinedAcc.Id);
        updated.Status.Should().Be(InboxRotationStatus.Cooling);
        updated.ThawedAtUtc.Should().NotBeNull();
        updated.QuarantineReason.Should().BeNull();
    }

    [Fact]
    public async Task AuditPoolHealthAsync_WhenHealthyAndActive_DoesNotModifyState()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var healthyAcc = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "healthy@sgrowing.io",
            Status = InboxRotationStatus.Active,
            LastDispatchedAtUtc = now.AddHours(-1), // recent
            CreatedAtUtc = now.AddMonths(-1),
            HealthScore = 98
        };

        db.SenderAccounts.Add(healthyAcc);
        await db.SaveChangesAsync();

        var result = await MultiInboxDispatchDaemon.AuditPoolHealthAsync(db, referenceTimeUtc: now);

        result.DormantInboxesCooled.Should().Be(0);
        result.WarmupAccountsPromoted.Should().Be(0);
        result.QuarantinedAccountsRecovered.Should().Be(0);

        var updated = await db.SenderAccounts.FirstAsync(a => a.Id == healthyAcc.Id);
        updated.Status.Should().Be(InboxRotationStatus.Active);
    }

    [Fact]
    public async Task ResetMidnightCountersAsync_WhenNoAccounts_ReturnsZeroAndDoesNotThrow()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;

        var resetCount = await MultiInboxDispatchDaemon.ResetMidnightCountersAsync(db);

        resetCount.Should().Be(0);
    }

    [Fact]
    public async Task AuditPoolHealthAsync_WhenWarmupAccountYoungerThan14DaysOrHighBounceRate_DoesNotPromote()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Young account (< 14 days)
        var youngAcc = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "young@sgrowing.io",
            Status = InboxRotationStatus.Warmup,
            CreatedAtUtc = now.AddDays(-10),
            HealthScore = 95,
            TotalDispatched = 100,
            TotalDelivered = 100,
            TotalBounced = 0
        };

        // Old account with high bounce rate (> 1.0%)
        var highBounceAcc = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "bouncy@sgrowing.io",
            Status = InboxRotationStatus.Warmup,
            CreatedAtUtc = now.AddDays(-20),
            HealthScore = 92,
            TotalDispatched = 100,
            TotalDelivered = 95,
            TotalBounced = 5 // BounceRate = 5.0%
        };

        db.SenderAccounts.AddRange(youngAcc, highBounceAcc);
        await db.SaveChangesAsync();

        var result = await MultiInboxDispatchDaemon.AuditPoolHealthAsync(db, referenceTimeUtc: now);

        result.WarmupAccountsPromoted.Should().Be(0);

        var verifyYoung = await db.SenderAccounts.FirstAsync(a => a.Id == youngAcc.Id);
        verifyYoung.Status.Should().Be(InboxRotationStatus.Warmup);

        var verifyBounce = await db.SenderAccounts.FirstAsync(a => a.Id == highBounceAcc.Id);
        verifyBounce.Status.Should().Be(InboxRotationStatus.Warmup);
    }

    [Fact]
    public async Task AuditPoolHealthAsync_WhenQuarantinedMissingDkimOrDmarc_DoesNotRecover()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var failedDkimAcc = new SenderAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "failed-dkim@sgrowing.io",
            Status = InboxRotationStatus.Quarantined,
            QuarantinedAtUtc = now.AddDays(-5),
            HasSpf = true,
            HasDkim = false,
            HasDmarc = true
        };

        db.SenderAccounts.Add(failedDkimAcc);
        await db.SaveChangesAsync();

        var result = await MultiInboxDispatchDaemon.AuditPoolHealthAsync(db, referenceTimeUtc: now);

        result.QuarantinedAccountsRecovered.Should().Be(0);

        var updated = await db.SenderAccounts.FirstAsync(a => a.Id == failedDkimAcc.Id);
        updated.Status.Should().Be(InboxRotationStatus.Quarantined);
    }
}
