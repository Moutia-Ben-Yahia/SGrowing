using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class PublicationCmsAndPaywallTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly PublicationCmsService _service;

    public PublicationCmsAndPaywallTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _outboxMock = new Mock<IOutbox>();
        _service = new PublicationCmsService(_db, _outboxMock.Object);
    }

    [Fact]
    public async Task CreateArticle_CalculatesWordCountAndEstimatedReadTime()
    {
        var tenantId = Guid.NewGuid();
        // Generate ~400 words
        var words = string.Join(" ", Enumerable.Repeat("growth marketing newsletter optimization", 100));
        var html = $"<p>{words}</p>";

        var article = await _service.CreateArticleAsync(
            tenantId,
            "The 2026 SaaS Retention Playbook",
            "saas-retention-playbook",
            "Complete breakdown of customer retention strategies.",
            html,
            authorName: "Sarah Connor",
            visibility: PublicationVisibility.Public,
            lexicalAstJson: "{\"root\":{\"children\":[]}}"
        );

        Assert.NotNull(article);
        Assert.Equal(PublicationArticleStatus.Draft, article.Status);
        Assert.True(article.EstimatedReadMinutes >= 2);
    }

    [Fact]
    public async Task PublishArticle_TransitionsStateAndSetsTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var article = await _service.CreateArticleAsync(
            tenantId,
            "Engineering Velocity Metrics",
            "engineering-velocity",
            "Excerpt",
            "<p>Some short text here.</p>"
        );

        var published = await _service.PublishArticleAsync(tenantId, article.Id);

        Assert.Equal(PublicationArticleStatus.Published, published.Status);
        Assert.NotNull(published.PublishedAtUtc);
    }

    [Fact]
    public async Task RenderArticle_PaywallEnforcement_BlocksAnonymousFromPaidArticle()
    {
        var tenantId = Guid.NewGuid();
        var longHtml = "<p>Introduction to high-yield strategies.</p>" +
                       "<p>" + string.Join(" ", Enumerable.Repeat("Confidential proprietary insider insight.", 50)) + "</p>";

        var article = await _service.CreateArticleAsync(
            tenantId,
            "Exclusive Insider Analysis",
            "exclusive-insider",
            "Paid members only report",
            longHtml,
            visibility: PublicationVisibility.PaidTier
        );
        await _service.PublishArticleAsync(tenantId, article.Id);

        // 1. Anonymous reader -> Paywalled with teaser & banner
        var anonymousView = await _service.RenderArticleForReaderAsync(tenantId, "exclusive-insider", isReaderMember: false, isReaderPaid: false);
        Assert.NotNull(anonymousView);
        Assert.True(anonymousView.IsPaywalled);
        Assert.Contains("paywall-lock-banner", anonymousView.RenderedHtml);
        Assert.NotNull(anonymousView.PaywallCtaMessage);
        Assert.Contains("Upgrade to our Premium Membership", anonymousView.PaywallCtaMessage);

        // 2. Paid reader -> Full access
        var paidView = await _service.RenderArticleForReaderAsync(tenantId, "exclusive-insider", isReaderMember: true, isReaderPaid: true);
        Assert.NotNull(paidView);
        Assert.False(paidView.IsPaywalled);
        Assert.Null(paidView.PaywallCtaMessage);
        Assert.DoesNotContain("paywall-lock-banner", paidView.RenderedHtml);
        Assert.Contains("Confidential proprietary insider insight", paidView.RenderedHtml);
    }

    [Fact]
    public async Task RenderArticle_MembersOnly_AllowsMemberReader()
    {
        var tenantId = Guid.NewGuid();
        var article = await _service.CreateArticleAsync(
            tenantId,
            "Community Weekly Memo",
            "community-memo",
            "Free member memo",
            "<p>Secret community link here.</p>",
            visibility: PublicationVisibility.MembersOnly
        );
        await _service.PublishArticleAsync(tenantId, article.Id);

        // Anonymous blocked
        var anon = await _service.RenderArticleForReaderAsync(tenantId, "community-memo", isReaderMember: false);
        Assert.NotNull(anon);
        Assert.True(anon.IsPaywalled);

        // Member allowed
        var member = await _service.RenderArticleForReaderAsync(tenantId, "community-memo", isReaderMember: true);
        Assert.NotNull(member);
        Assert.False(member.IsPaywalled);
    }

    [Fact]
    public async Task BroadcastArticleNewsletter_DispatchesOutboxEventAndRecordsSentCount()
    {
        var tenantId = Guid.NewGuid();
        var article = await _service.CreateArticleAsync(tenantId, "Newsletter Issue #42", "issue-42", "Weekly wrap", "<p>Content</p>");
        await _service.PublishArticleAsync(tenantId, article.Id);

        var broadcast = await _service.BroadcastArticleNewsletterAsync(
            tenantId,
            article.Id,
            "Issue #42: Growth Flywheels",
            "AllSubscribers",
            estimatedAudience: 2000
        );

        Assert.NotNull(broadcast);
        Assert.Equal(NewsletterBroadcastStatus.Completed, broadcast.Status);
        Assert.Equal(2000, broadcast.TotalRecipientsCount);
        Assert.True(broadcast.DeliveredCount > 1900);
        Assert.NotNull(broadcast.SentAtUtc);

        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "Publication.NewsletterDispatched",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
