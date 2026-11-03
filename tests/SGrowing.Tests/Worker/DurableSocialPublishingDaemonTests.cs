using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class DurableSocialPublishingDaemonTests
{
    private static (IServiceProvider Provider, SGrowingDbContext Db) SetupDependencies()
    {
        var services = new ServiceCollection();
        var dbName = $"DurableSocialTests_{Guid.NewGuid()}";
        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        return (sp, db);
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenNoPosts_ReturnsZero()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenPostsScheduledInFuture_SkipsThem()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var futurePost = SocialPost.Create(
            tenantId,
            "Future product launch announcement #growth",
            new List<SocialPlatform> { SocialPlatform.LinkedIn, SocialPlatform.X },
            scheduledAtUtc: DateTime.UtcNow.AddDays(2));
        futurePost.Status = SocialPostStatus.Scheduled;

        db.SocialPosts.Add(futurePost);
        await db.SaveChangesAsync();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(0);
        var postInDb = await db.SocialPosts.FindAsync(futurePost.Id);
        postInDb!.Status.Should().Be(SocialPostStatus.Scheduled);
        postInDb.PublishedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenPostsDue_PublishesAndStampsTimestamp()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var duePost = SocialPost.Create(
            tenantId,
            "Live flash deal! 20% off for the next 2 hours #ecommerce",
            new List<SocialPlatform> { SocialPlatform.Instagram, SocialPlatform.Facebook },
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-5));
        duePost.Status = SocialPostStatus.Scheduled;

        db.SocialPosts.Add(duePost);
        await db.SaveChangesAsync();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(1);
        var postInDb = await db.SocialPosts.FindAsync(duePost.Id);
        postInDb!.Status.Should().Be(SocialPostStatus.Published);
        postInDb.PublishedAtUtc.Should().NotBeNull();
        postInDb.PublishedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenMoreThan15Due_LimitsBatchTo15()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        for (int i = 0; i < 20; i++)
        {
            var p = SocialPost.Create(
                tenantId,
                $"Omnichannel blast #{i}",
                new List<SocialPlatform> { SocialPlatform.X },
                scheduledAtUtc: DateTime.UtcNow.AddMinutes(-10 - i));
            p.Status = SocialPostStatus.Scheduled;
            db.SocialPosts.Add(p);
        }
        await db.SaveChangesAsync();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(15);
        var remainingScheduled = await db.SocialPosts.CountAsync(p => p.Status == SocialPostStatus.Scheduled);
        remainingScheduled.Should().Be(5);
        var publishedCount = await db.SocialPosts.CountAsync(p => p.Status == SocialPostStatus.Published);
        publishedCount.Should().Be(15);
    }

    [Fact]
    public async Task AuditTokenExpirationHealthAsync_WhenTokensValidBeyond7Days_ReturnsZero()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var healthyAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.LinkedIn,
            "Enterprise Marketing Lead",
            "ext-in-101",
            accessToken: "valid-token-xyz",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(30));

        db.SocialAccounts.Add(healthyAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db, NullLogger.Instance);

        flagged.Should().Be(0);
        var account = await db.SocialAccounts.FindAsync(healthyAccount.Id);
        account!.RefreshTokenRequired.Should().BeFalse();
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditTokenExpirationHealthAsync_WhenAccountInactive_SkipsEvenIfExpiring()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var inactiveAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.Instagram,
            "Dormant Handle",
            "ext-ig-999",
            accessToken: "expired-token",
            isActive: false,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(1));

        db.SocialAccounts.Add(inactiveAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db, NullLogger.Instance);

        flagged.Should().Be(0);
        var account = await db.SocialAccounts.FindAsync(inactiveAccount.Id);
        account!.RefreshTokenRequired.Should().BeFalse();
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditTokenExpirationHealthAsync_WhenAlreadyFlagged_DoesNotDuplicate()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var alreadyFlaggedAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.Facebook,
            "Brand Page",
            "ext-fb-333",
            accessToken: "expiring-token",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(2),
            refreshTokenRequired: true);

        db.SocialAccounts.Add(alreadyFlaggedAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db, NullLogger.Instance);

        flagged.Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditTokenExpirationHealthAsync_WhenExpiringWithin7Days_FlagsAndEmitsOutboxMessage()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var expiringAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.Threads,
            "Growth SGrowing",
            "ext-th-444",
            accessToken: "soon-to-expire",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(3));

        db.SocialAccounts.Add(expiringAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db, NullLogger.Instance);

        flagged.Should().Be(1);
        var accountInDb = await db.SocialAccounts.FindAsync(expiringAccount.Id);
        accountInDb!.RefreshTokenRequired.Should().BeTrue();

        var outboxList = await db.OutboxMessages.ToListAsync();
        outboxList.Should().HaveCount(1);
        var msg = outboxList[0];
        msg.EventType.Should().Be("SocialAccount.TokenExpirationImminent");
        msg.Payload.Should().Contain("Growth SGrowing");
        msg.Payload.Should().Contain("Threads");
        msg.Payload.Should().Contain("\"IsExpired\":false");
    }

    [Fact]
    public async Task AuditTokenExpirationHealthAsync_WhenAlreadyExpired_FlagsWithExpiredAlertMessage()
    {
        var (_, db) = SetupDependencies();
        using var scope = db;
        var tenantId = Guid.NewGuid();

        var expiredAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.TikTok,
            "Viral Campaigns",
            "ext-tt-777",
            accessToken: "stale-token",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddHours(-12));

        db.SocialAccounts.Add(expiredAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db, NullLogger.Instance);

        flagged.Should().Be(1);
        var accountInDb = await db.SocialAccounts.FindAsync(expiredAccount.Id);
        accountInDb!.RefreshTokenRequired.Should().BeTrue();

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Payload.Should().Contain("\"IsExpired\":true");
        outbox.Payload.Should().Contain("has expired");
    }
}
