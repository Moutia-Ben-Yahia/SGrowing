using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class SocialPublishingTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public void SocialEntities_FactoryMethods_InstantiateCorrectly()
    {
        var tenantId = Guid.NewGuid();

        // 1. SocialAccount
        var account = SocialAccount.Create(
            tenantId,
            SocialPlatform.LinkedIn,
            "SGrowing Corporate",
            "act_linkedin_12345",
            "token_xyz"
        );

        account.Id.Should().NotBeEmpty();
        account.TenantId.Should().Be(tenantId);
        account.Platform.Should().Be(SocialPlatform.LinkedIn);
        account.AccountName.Should().Be("SGrowing Corporate");
        account.ExternalAccountId.Should().Be("act_linkedin_12345");
        account.AccessToken.Should().Be("token_xyz");
        account.IsActive.Should().BeTrue();
        account.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // 2. SocialPost Scheduled
        var futureDate = DateTime.UtcNow.AddHours(2);
        var post = SocialPost.Create(
            tenantId,
            "Major update announcement!",
            new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn },
            scheduledAtUtc: futureDate,
            mediaUrls: new List<string> { "https://cdn.sgrowing.com/img1.png" }
        );

        post.Id.Should().NotBeEmpty();
        post.TenantId.Should().Be(tenantId);
        post.Content.Should().Be("Major update announcement!");
        post.Status.Should().Be(SocialPostStatus.Scheduled);
        post.ScheduledAtUtc.Should().Be(futureDate);
        post.PublishedAtUtc.Should().BeNull();
        post.TargetPlatforms.Should().Contain(SocialPlatform.X);
        post.TargetPlatforms.Should().Contain(SocialPlatform.LinkedIn);
        post.MediaUrls.Should().ContainSingle().Which.Should().Be("https://cdn.sgrowing.com/img1.png");

        // 3. SocialPost Draft
        var draftPost = SocialPost.Create(
            tenantId,
            "Draft idea",
            new List<SocialPlatform> { SocialPlatform.Facebook }
        );
        draftPost.Status.Should().Be(SocialPostStatus.Draft);
        draftPost.ScheduledAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CreatePostAsync_PersistsPostAndReturnsEntity()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new SocialPublishingService(outbox);
        var tenantId = Guid.NewGuid();

        var scheduledTime = DateTime.UtcNow.AddMinutes(30);
        var post = await service.CreatePostAsync(
            tenantId,
            "Join our live webinar on Growth Marketing!",
            new List<SocialPlatform> { SocialPlatform.LinkedIn, SocialPlatform.Instagram },
            scheduledTime,
            new List<string> { "https://media.sgrowing.com/webinar.jpg" }
        );

        post.Should().NotBeNull();
        post.TenantId.Should().Be(tenantId);
        post.Status.Should().Be(SocialPostStatus.Scheduled);
        post.ScheduledAtUtc.Should().Be(scheduledTime);

        var retrieved = await service.Repository.GetByIdAsync(post.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Content.Should().Be("Join our live webinar on Growth Marketing!");
    }

    [Fact]
    public async Task PublishDuePostsAsync_PublishesDuePostsAndEnqueuesOutboxMessages()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new SocialPublishingService(db, outbox);
        var tenantId = Guid.NewGuid();

        // 1. Due post across multiple platforms
        var duePost = await service.CreatePostAsync(
            tenantId,
            "Exciting product release for .NET 10!",
            new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn, SocialPlatform.Telegram },
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-5) // Past time -> Due
        );

        // 2. Future post not yet due
        var futurePost = await service.CreatePostAsync(
            tenantId,
            "Future roadmap sneak peek",
            new List<SocialPlatform> { SocialPlatform.Facebook },
            scheduledAtUtc: DateTime.UtcNow.AddHours(3) // Future time -> Not due
        );

        // Act
        var publishedList = await service.PublishDuePostsAsync(tenantId);

        // Assert post statuses
        publishedList.Should().ContainSingle(p => p.Id == duePost.Id);

        var updatedDuePost = await service.Repository.GetByIdAsync(duePost.Id);
        updatedDuePost!.Status.Should().Be(SocialPostStatus.Published);
        updatedDuePost.PublishedAtUtc.Should().NotBeNull();

        var updatedFuturePost = await service.Repository.GetByIdAsync(futurePost.Id);
        updatedFuturePost!.Status.Should().Be(SocialPostStatus.Scheduled);
        updatedFuturePost.PublishedAtUtc.Should().BeNull();

        // Assert Outbox messages enqueued
        var outboxMessages = await db.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.EventType == "SocialPost.Published")
            .ToListAsync();

        outboxMessages.Should().HaveCount(3); // One per target platform (X, LinkedIn, Telegram)

        // Check LinkedIn payload contains hashtags
        var linkedInMsg = outboxMessages.Single(m => m.Payload.Contains("LinkedIn"));
        linkedInMsg.Payload.Should().Contain("#Business");
        linkedInMsg.Payload.Should().Contain("#Growth");

        // Check Telegram payload contains announcement broadcast emoji
        var telegramMsg = outboxMessages.Single(m => m.Payload.Contains("Telegram"));
        var telegramPayload = JsonSerializer.Deserialize<SocialPostPublishedPayload>(telegramMsg.Payload);
        telegramPayload.Should().NotBeNull();
        telegramPayload!.Content.Should().StartWith("📢");

        // Check X message is included
        var xMsg = outboxMessages.Single(m => m.Payload.Contains("\"PlatformName\":\"X\"") || m.Payload.Contains("Platform\":4"));
        xMsg.Payload.Should().Contain("Exciting product release for .NET 10!");
    }

    [Fact]
    public async Task PublishDuePostsAsync_CustomizesLongContentForXCharacterLimit()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var service = new SocialPublishingService(outbox);
        var tenantId = Guid.NewGuid();

        var longContent = new string('A', 350);
        await service.CreatePostAsync(
            tenantId,
            longContent,
            new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn },
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-1)
        );

        await service.PublishDuePostsAsync(tenantId);

        var outboxMessages = await db.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.EventType == "SocialPost.Published")
            .ToListAsync();

        outboxMessages.Should().HaveCount(2);

        var xMsg = outboxMessages.Single(m => m.Payload.Contains("PlatformName\":\"X\"") || m.Payload.Contains("Platform\":4"));
        var xPayload = JsonSerializer.Deserialize<SocialPostPublishedPayload>(xMsg.Payload);
        xPayload.Should().NotBeNull();
        xPayload!.Content.Length.Should().Be(280);
        xPayload.Content.Should().EndWith("...");
    }

    [Theory]
    [InlineData("Can you share the price of the enterprise tier?")]
    [InlineData("I would love to see a demo for our marketing team")]
    [InlineData("Where can I buy or order this product?")]
    [InlineData("Need urgent help with customer onboarding")]
    [InlineData("Where can I find more info regarding integrations?")]
    [InlineData("Please send me the link to signup")]
    public async Task HandlePublicCommentAsync_WithPurchaseOrSupportIntent_DeflectsToPrivateDm(string commentText)
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var deflectionService = new SocialCommentDeflectionService(db, outbox);
        var tenantId = Guid.NewGuid();

        var result = await deflectionService.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.Instagram,
            postExternalId: "ig_post_999",
            commenterUsername: "alice_growth",
            commentText: commentText
        );

        result.Deflected.Should().BeTrue();
        result.ReplyText.Should().Be("Sent you a DM with details!");

        var outboxMessages = await db.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.EventType == "Social.DirectMessage.Send")
            .ToListAsync();

        outboxMessages.Should().ContainSingle();
        var dm = outboxMessages.First();
        dm.Payload.Should().Contain("alice_growth");
        dm.Payload.Should().Contain("ig_post_999");
        dm.Payload.Should().Contain("Instagram");
    }

    [Theory]
    [InlineData("This looks awesome, keep up the great work!")]
    [InlineData("Congratulations to the whole team!")]
    [InlineData("Love the new UI design!")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandlePublicCommentAsync_WithoutIntent_DoesNotDeflect(string commentText)
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var deflectionService = new SocialCommentDeflectionService(outbox);
        var tenantId = Guid.NewGuid();

        var result = await deflectionService.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.LinkedIn,
            postExternalId: "li_post_888",
            commenterUsername: "bob_dev",
            commentText: commentText
        );

        result.Deflected.Should().BeFalse();
        result.ReplyText.Should().BeNull();

        var outboxMessages = await db.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.EventType == "Social.DirectMessage.Send")
            .ToListAsync();

        outboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishDuePostsAsync_IsolatesTenants()
    {
        var db = CreateDbContext();
        var outbox = new OutboxService(db);
        var repo = new InMemorySocialPostRepository();
        var serviceA = new SocialPublishingService(outbox, repo);
        var serviceB = new SocialPublishingService(outbox, repo);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await serviceA.CreatePostAsync(tenantA, "Tenant A update", new List<SocialPlatform> { SocialPlatform.X }, DateTime.UtcNow.AddMinutes(-5));
        await serviceB.CreatePostAsync(tenantB, "Tenant B update", new List<SocialPlatform> { SocialPlatform.X }, DateTime.UtcNow.AddMinutes(-5));

        // Act: Publish only tenant A
        var publishedA = await serviceA.PublishDuePostsAsync(tenantA);

        publishedA.Should().ContainSingle(p => p.TenantId == tenantA);

        var allPostsB = await repo.GetAllAsync(tenantB);
        allPostsB.Should().ContainSingle(p => p.Status == SocialPostStatus.Scheduled);

        var outboxA = await db.OutboxMessages.Where(m => m.TenantId == tenantA).ToListAsync();
        outboxA.Should().ContainSingle();

        var outboxB = await db.OutboxMessages.Where(m => m.TenantId == tenantB).ToListAsync();
        outboxB.Should().BeEmpty();
    }
}
