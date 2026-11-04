using FluentAssertions;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class SocialCommentDeflectionServiceTests
{
    private class FakeOutbox : IOutbox
    {
        public List<(Guid TenantId, string EventType, object Payload)> EnqueuedMessages { get; } = new();

        public Task EnqueueAsync<TEvent>(Guid tenantId, string eventType, TEvent payload, CancellationToken cancellationToken = default) where TEvent : class
        {
            EnqueuedMessages.Add((tenantId, eventType, payload));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task HandlePublicCommentAsync_IntentKeywordPresent_EnqueuesDmAndReturnsDeflectionReply()
    {
        var outbox = new FakeOutbox();
        var service = new SocialCommentDeflectionService(outbox);
        var tenantId = Guid.NewGuid();

        var result = await service.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.Instagram,
            "post_reel_99",
            "sarah_runner",
            "What is the price of this subscription?"
        );

        result.Should().NotBeNull();
        result.Deflected.Should().BeTrue();
        result.ReplyText.Should().Be("Sent you a DM with details!");

        outbox.EnqueuedMessages.Should().ContainSingle();
        var msg = outbox.EnqueuedMessages[0];
        msg.TenantId.Should().Be(tenantId);
        msg.EventType.Should().Be("Social.DirectMessage.Send");

        var payload = msg.Payload as SocialDirectMessageTaskPayload;
        payload.Should().NotBeNull();
        payload!.Platform.Should().Be(SocialPlatform.Instagram);
        payload.CommenterUsername.Should().Be("sarah_runner");
        payload.DirectMessageText.Should().Contain("@sarah_runner");
    }

    [Theory]
    [InlineData("How much does this cost?")]
    [InlineData("Can I get a demo?")]
    [InlineData("Where can I buy this?")]
    [InlineData("I need help with my account")]
    [InlineData("Please send me the link")]
    public async Task HandlePublicCommentAsync_VariousIntentKeywords_TriggersDeflection(string commentText)
    {
        var outbox = new FakeOutbox();
        var service = new SocialCommentDeflectionService(outbox);
        var tenantId = Guid.NewGuid();

        var result = await service.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.Facebook,
            "post_fb_123",
            "alex_buyer",
            commentText
        );

        result.Deflected.Should().BeTrue();
        result.ReplyText.Should().Be("Sent you a DM with details!");
        outbox.EnqueuedMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task HandlePublicCommentAsync_NoIntentKeyword_DoesNotDeflectOrEnqueue()
    {
        var outbox = new FakeOutbox();
        var service = new SocialCommentDeflectionService(outbox);
        var tenantId = Guid.NewGuid();

        var result = await service.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.TikTok,
            "tt_video_88",
            "cool_dancer",
            "Great video! Loved the background music!"
        );

        result.Deflected.Should().BeFalse();
        result.ReplyText.Should().BeNull();
        outbox.EnqueuedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandlePublicCommentAsync_EmptyOrWhitespaceComment_ReturnsNonDeflected()
    {
        var outbox = new FakeOutbox();
        var service = new SocialCommentDeflectionService(outbox);
        var tenantId = Guid.NewGuid();

        var emptyResult = await service.HandlePublicCommentAsync(tenantId, SocialPlatform.YouTube, "yt_01", "user1", "");
        var wsResult = await service.HandlePublicCommentAsync(tenantId, SocialPlatform.YouTube, "yt_01", "user1", "   ");

        emptyResult.Deflected.Should().BeFalse();
        wsResult.Deflected.Should().BeFalse();
        outbox.EnqueuedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandlePublicCommentAsync_EmptyTenantId_ThrowsArgumentException()
    {
        var outbox = new FakeOutbox();
        var service = new SocialCommentDeflectionService(outbox);

        var act = async () => await service.HandlePublicCommentAsync(
            Guid.Empty,
            SocialPlatform.Instagram,
            "post_01",
            "user",
            "how much?"
        );

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TenantId is required*");
    }
}
