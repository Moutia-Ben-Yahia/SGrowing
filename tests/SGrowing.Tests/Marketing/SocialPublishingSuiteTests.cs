using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class SocialPublishingSuiteTests
{
    private readonly Mock<IOutbox> _outboxMock = new();

    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CreatePostAsync_CreatesAndPersistsScheduledPostWithMedia()
    {
        // Arrange
        var service = new SocialPublishingService(_outboxMock.Object);
        var tenantId = Guid.NewGuid();
        var content = "Discover our brand new omnichannel growth platform with native AI SDR!";
        var platforms = new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn, SocialPlatform.Telegram };
        var scheduledAt = DateTime.UtcNow.AddMinutes(30);
        var media = new List<string> { "https://cdn.sgrowing.io/media1.jpg", "https://cdn.sgrowing.io/media2.jpg" };

        // Act
        var post = await service.CreatePostAsync(tenantId, content, platforms, scheduledAt, media);

        // Assert
        post.Should().NotBeNull();
        post.TenantId.Should().Be(tenantId);
        post.Content.Should().Be(content);
        post.Status.Should().Be(SocialPostStatus.Scheduled);
        post.TargetPlatforms.Should().BeEquivalentTo(platforms);
        post.MediaUrls.Should().BeEquivalentTo(media);
        post.ScheduledAtUtc.Should().Be(scheduledAt);

        var retrieved = await service.Repository.GetByIdAsync(post.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(post.Id);
    }

    [Fact]
    public async Task PublishDuePostsAsync_PublishesDuePostsAndEnqueuesOutboxEvents()
    {
        // Arrange
        var service = new SocialPublishingService(_outboxMock.Object);
        var tenantId = Guid.NewGuid();
        var content = "Time-sensitive product launch announcement!";
        var platforms = new List<SocialPlatform> { SocialPlatform.X, SocialPlatform.LinkedIn };

        // Create a post whose scheduled time is in the past
        var duePost = await service.CreatePostAsync(
            tenantId,
            content,
            platforms,
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-10),
            mediaUrls: new List<string> { "https://cdn.sgrowing.io/launch.png" }
        );

        // Create another post in the future (not due)
        var futurePost = await service.CreatePostAsync(
            tenantId,
            "Future update",
            platforms,
            scheduledAtUtc: DateTime.UtcNow.AddHours(2)
        );

        // Act
        var published = await service.PublishDuePostsAsync(tenantId);

        // Assert
        published.Should().HaveCount(1);
        published[0].Id.Should().Be(duePost.Id);
        published[0].Status.Should().Be(SocialPostStatus.Published);
        published[0].PublishedAtUtc.Should().NotBeNull();

        // Verify outbox was called for each target platform
        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "SocialPost.Published",
            It.Is<SocialPostPublishedPayload>(p => p.PostId == duePost.Id && p.Platform == SocialPlatform.X),
            default),
            Times.Once);

        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "SocialPost.Published",
            It.Is<SocialPostPublishedPayload>(p => p.PostId == duePost.Id && p.Platform == SocialPlatform.LinkedIn),
            default),
            Times.Once);

        // Future post remains scheduled
        var futureRetrieved = await service.Repository.GetByIdAsync(futurePost.Id);
        futureRetrieved!.Status.Should().Be(SocialPostStatus.Scheduled);
    }

    [Theory]
    [InlineData(SocialPlatform.X)]
    [InlineData(SocialPlatform.LinkedIn)]
    [InlineData(SocialPlatform.Telegram)]
    [InlineData(SocialPlatform.Instagram)]
    public void CustomizeContentForPlatform_AppliesChannelSpecificFormatting(SocialPlatform platform)
    {
        var rawText = "Check out our autonomous inbound/outbound sales engine.";
        var customized = SocialPublishingService.CustomizeContentForPlatform(rawText, platform);

        switch (platform)
        {
            case SocialPlatform.X:
                customized.Should().Be(rawText);
                break;
            case SocialPlatform.LinkedIn:
                customized.Should().Contain("#Business #Growth #Marketing");
                break;
            case SocialPlatform.Telegram:
                customized.Should().StartWith("📢 ");
                break;
            case SocialPlatform.Instagram:
                customized.Should().Contain("#marketing #growth #socialmedia");
                break;
        }
    }

    [Fact]
    public void CustomizeContentForPlatform_WhenOverXCharacterLimit_TruncatesWithEllipsis()
    {
        var longText = new string('A', 350);
        var truncated = SocialPublishingService.CustomizeContentForPlatform(longText, SocialPlatform.X);

        truncated.Length.Should().Be(280);
        truncated.Should().EndWith("...");
    }

    [Fact]
    public async Task DeflectCommentToDm_WhenIntentKeywordPresent_EnqueuesDmAndReturnsTrue()
    {
        // Arrange
        var service = new SocialCommentDeflectionService(_outboxMock.Object);
        var tenantId = Guid.NewGuid();

        // Act
        var result = await service.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.Instagram,
            "post_insta_99",
            "future_customer",
            "What is the pricing for high volume team accounts? Can you share a demo link?"
        );

        // Assert
        result.Deflected.Should().BeTrue();
        result.ReplyText.Should().Contain("DM with details");

        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "Social.DirectMessage.Send",
            It.Is<SocialDirectMessageTaskPayload>(p =>
                p.CommenterUsername == "future_customer" &&
                p.Platform == SocialPlatform.Instagram &&
                p.DirectMessageText.Contains("details")),
            default),
            Times.Once);
    }

    [Fact]
    public async Task DeflectCommentToDm_WhenNoIntentKeyword_IgnoresAndReturnsFalse()
    {
        // Arrange
        var service = new SocialCommentDeflectionService(_outboxMock.Object);
        var tenantId = Guid.NewGuid();

        // Act
        var result = await service.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.X,
            "post_x_10",
            "casual_viewer",
            "Looks wonderful, love the color palette! 🔥"
        );

        // Assert
        result.Deflected.Should().BeFalse();
        result.ReplyText.Should().BeNull();

        _outboxMock.Verify(o => o.EnqueueAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            default),
            Times.Never);
    }
}
