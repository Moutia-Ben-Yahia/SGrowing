using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class DurableSocialPublishingDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenNoDuePosts_ReturnsZero()
    {
        using var db = CreateDbContext();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenPostDueInFuture_DoesNotPublish()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var post = SocialPost.Create(
            tenantId,
            "Excited to announce our new product line!",
            new List<SocialPlatform> { SocialPlatform.LinkedIn, SocialPlatform.X },
            scheduledAtUtc: DateTime.UtcNow.AddHours(2)
        );
        post.Status = SocialPostStatus.Scheduled;

        db.SocialPosts.Add(post);
        await db.SaveChangesAsync();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(0);
        post.Status.Should().Be(SocialPostStatus.Scheduled);
        post.PublishedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenPostDue_PublishesAndStampsTimestamp()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var post = SocialPost.Create(
            tenantId,
            "Live webinar starting now!",
            new List<SocialPlatform> { SocialPlatform.LinkedIn, SocialPlatform.Instagram },
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-5)
        );
        post.Status = SocialPostStatus.Scheduled;

        db.SocialPosts.Add(post);
        await db.SaveChangesAsync();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(1);

        var updated = await db.SocialPosts.FindAsync(post.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SocialPostStatus.Published);
        updated.PublishedAtUtc.Should().NotBeNull();
        updated.PublishedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProcessScheduledSocialPostsAsync_WhenPostIsDraftOrFailed_DoesNotProcess()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var draftPost = SocialPost.Create(
            tenantId,
            "Draft idea for next week",
            new List<SocialPlatform> { SocialPlatform.Facebook },
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-10)
        );
        draftPost.Status = SocialPostStatus.Draft;

        var failedPost = SocialPost.Create(
            tenantId,
            "Failed post from yesterday",
            new List<SocialPlatform> { SocialPlatform.X },
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-10)
        );
        failedPost.Status = SocialPostStatus.Failed;

        db.SocialPosts.AddRange(draftPost, failedPost);
        await db.SaveChangesAsync();

        var processed = await DurableSocialPublishingDaemon.ProcessScheduledSocialPostsAsync(db);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task AuditTokenExpirationHealthAsync_WhenTokenExpiringWithin7Days_FlagsRefreshTokenRequiredAndQueuesOutbox()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var expiringAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.LinkedIn,
            "Acme Corp Official",
            "urn:li:organization:12345",
            accessToken: "valid_token_xyz",
            isActive: true,
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(3),
            refreshTokenRequired: false
        );

        db.SocialAccounts.Add(expiringAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db);

        flagged.Should().Be(1);

        var updated = await db.SocialAccounts.FindAsync(expiringAccount.Id);
        updated.Should().NotBeNull();
        updated!.RefreshTokenRequired.Should().BeTrue();

        var outboxMessage = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "SocialAccount.TokenExpirationImminent");
        outboxMessage.Should().NotBeNull();
        outboxMessage!.Payload.Should().Contain("Acme Corp Official");
        outboxMessage.Payload.Should().Contain("LinkedIn");
    }

    [Fact]
    public async Task AuditTokenExpirationHealthAsync_WhenTokenHealthyOrAlreadyFlagged_IgnoresAccount()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var healthyAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.Instagram,
            "@acme_brand",
            "ig_user_789",
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(45), // Healthy
            refreshTokenRequired: false
        );

        var alreadyFlaggedAccount = SocialAccount.Create(
            tenantId,
            SocialPlatform.X,
            "@acme_tech",
            "x_user_555",
            tokenExpiresAtUtc: DateTime.UtcNow.AddDays(2),
            refreshTokenRequired: true // Already flagged
        );

        db.SocialAccounts.AddRange(healthyAccount, alreadyFlaggedAccount);
        await db.SaveChangesAsync();

        var flagged = await DurableSocialPublishingDaemon.AuditTokenExpirationHealthAsync(db);

        flagged.Should().Be(0);
        db.OutboxMessages.Should().BeEmpty();
    }
}
