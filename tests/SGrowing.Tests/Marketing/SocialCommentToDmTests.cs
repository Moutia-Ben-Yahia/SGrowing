using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class SocialCommentToDmTests
{
    private readonly ISocialCommentToDmService _service = new SocialCommentToDmService();

    [Fact]
    public void NormalizeComment_NormalizesPlatforms_AndStripsRedundantWhitespace()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var incoming = new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "instagram",
            PostId: "reel_123",
            CommentId: "cmt_456",
            CommenterId: "user_789",
            CommenterUsername: "@alex_growth",
            CommentText: "   HOW MUCH    is this    product?   "
        );

        // Act
        var normalized = _service.NormalizeComment(incoming);

        // Assert
        normalized.Should().NotBeNull();
        normalized.Platform.Should().Be(SocialPlatform.Instagram);
        normalized.CommenterUsername.Should().Be("alex_growth");
        normalized.CleanText.Should().Be("HOW MUCH is this product?");
        normalized.OriginalText.Should().Be("   HOW MUCH    is this    product?   ");
    }

    [Theory]
    [InlineData("tiktok", SocialPlatform.TikTok)]
    [InlineData("facebook", SocialPlatform.Facebook)]
    [InlineData("youtube", SocialPlatform.YouTube)]
    [InlineData("ig", SocialPlatform.Instagram)]
    public void ResolvePlatform_MapsAllFourKeyPlatformsAccurately(string input, SocialPlatform expected)
    {
        var resolved = SocialCommentToDmService.ResolvePlatform(input);
        resolved.Should().Be(expected);
    }

    [Fact]
    public void EvaluateRuleMatch_ExactMatch_MatchesExactWordOrPhrase_CaseInsensitive()
    {
        // Arrange
        var rule = new SocialCommentRule
        {
            MatchType = SocialRuleMatchType.Exact,
            Keywords = new List<string> { "COUPON", "PROMO", "CODE" }
        };

        // Act & Assert
        var (match1, kw1) = SocialCommentToDmService.EvaluateRuleMatch(rule, "coupon");
        match1.Should().BeTrue();
        kw1.Should().Be("COUPON");

        var (match2, kw2) = SocialCommentToDmService.EvaluateRuleMatch(rule, "send PROMO please");
        match2.Should().BeTrue();
        kw2.Should().Be("PROMO");

        var (match3, _) = SocialCommentToDmService.EvaluateRuleMatch(rule, "something else entirely");
        match3.Should().BeFalse();
    }

    [Fact]
    public void EvaluateRuleMatch_ContainsMatch_MatchesSubstrings()
    {
        // Arrange
        var rule = new SocialCommentRule
        {
            MatchType = SocialRuleMatchType.Contains,
            Keywords = new List<string> { "PRICE", "BUY", "HOW MUCH" }
        };

        // Act & Assert
        var (match1, kw1) = SocialCommentToDmService.EvaluateRuleMatch(rule, "Can you tell me how much this costs?");
        match1.Should().BeTrue();
        kw1.Should().Be("HOW MUCH");

        var (match2, kw2) = SocialCommentToDmService.EvaluateRuleMatch(rule, "Need to BUY this today!");
        match2.Should().BeTrue();
        kw2.Should().Be("BUY");

        var (match3, _) = SocialCommentToDmService.EvaluateRuleMatch(rule, "Just saying hello!");
        match3.Should().BeFalse();
    }

    [Fact]
    public void EvaluateRuleMatch_RegexMatch_MatchesPatterns()
    {
        // Arrange
        var rule = new SocialCommentRule
        {
            MatchType = SocialRuleMatchType.Regex,
            RegexPattern = @"(?i)\b(discount|voucher|sale)\b",
            Keywords = new List<string>()
        };

        // Act & Assert
        var (match1, _) = SocialCommentToDmService.EvaluateRuleMatch(rule, "Is there an active DISCOUNT code available?");
        match1.Should().BeTrue();

        var (match2, _) = SocialCommentToDmService.EvaluateRuleMatch(rule, "Check out the SALE now");
        match2.Should().BeTrue();

        var (match3, _) = SocialCommentToDmService.EvaluateRuleMatch(rule, "Random chatter with no keywords");
        match3.Should().BeFalse();
    }

    [Fact]
    public void EvaluateRuleMatch_IntentSentiment_MatchesBuyingIntent_AndRejectsNegativeSentiment()
    {
        // Arrange
        var rule = new SocialCommentRule
        {
            MatchType = SocialRuleMatchType.IntentSentiment,
            Keywords = new List<string> { "interested", "available" }
        };

        // Act & Assert - Commercial Intent
        var (positiveMatch, _) = SocialCommentToDmService.EvaluateRuleMatch(rule, "I am really interested in where to buy this!");
        positiveMatch.Should().BeTrue();

        // Act & Assert - Negative sentiment should be rejected even if commercial words are present
        var (negativeMatch, _) = SocialCommentToDmService.EvaluateRuleMatch(rule, "This is a scam don't buy, fake terrible product!");
        negativeMatch.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessCommentAsync_ExecutesTwoPhaseReaction_WithPublicReplyAndPrivateDm()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var incoming = new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "Instagram",
            PostId: "post_vip_reel_01",
            CommentId: "c_101",
            CommenterId: "user_alpha",
            CommenterUsername: "jessica_m",
            CommentText: "What is the PRICE of this? Need the LINK!",
            PostTitle: "VIP Autumn Drop"
        );

        // Act
        var result = await _service.ProcessCommentAsync(incoming);

        // Assert
        result.Should().NotBeNull();
        result.Matched.Should().BeTrue();
        result.Status.Should().Be(CommentProcessingStatus.DmDelivered);

        // Phase 1 Assertion: Public comment reply from rotated template pool
        result.PublicReply.Should().NotBeNullOrWhiteSpace();
        result.PublicReply.Should().Match(r =>
            r.Contains("DMs") || r.Contains("inbox") || r.Contains("message") || r.Contains("details"));

        // Phase 2 Assertion: Private DM with generated coupon & UTM checkout link
        result.PrivateDm.Should().NotBeNullOrWhiteSpace();
        result.CouponCode.Should().NotBeNullOrWhiteSpace();
        result.CouponCode.Should().StartWith("VIP-");
        result.CheckoutUrl.Should().Contain("https://sgrowing.com/checkout");
        result.CheckoutUrl.Should().Contain($"code={result.CouponCode}");
        result.CheckoutUrl.Should().Contain("utm_source=instagram");
        result.CheckoutUrl.Should().Contain("utm_medium=comment_to_dm");
        result.CheckoutUrl.Should().Contain("utm_content=post_vip_reel_01");

        // Jitter Assertion: Jitter within bounds [5, 30]
        result.JitterDelaySeconds.Should().BeInRange(5, 30);
    }

    [Fact]
    public async Task ProcessCommentAsync_EnforcesDeduplication_OneDmPerPostPerUser()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var postId = "post_dedup_test_99";
        var commenterId = "same_commenter_42";

        var comment1 = new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "TikTok",
            PostId: postId,
            CommentId: "cmt_1",
            CommenterId: commenterId,
            CommenterUsername: "tiktok_fan",
            CommentText: "PRICE please"
        );

        var comment2 = new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "TikTok",
            PostId: postId,
            CommentId: "cmt_2",
            CommenterId: commenterId,
            CommenterUsername: "tiktok_fan",
            CommentText: "Hey tell me the PRICE again"
        );

        // Act
        var res1 = await _service.ProcessCommentAsync(comment1);
        var res2 = await _service.ProcessCommentAsync(comment2);

        // Assert
        res1.Status.Should().Be(CommentProcessingStatus.DmDelivered);
        res1.PrivateDm.Should().NotBeNull();

        // 2nd comment on same post from same commenter must be DuplicateSkipped
        res2.Status.Should().Be(CommentProcessingStatus.DuplicateSkipped);
        res2.PrivateDm.Should().BeNull();
        res2.PublicReply.Should().NotBeNull(); // Public reply is still sent to engage the algorithm
        res2.Message.Should().Contain("Duplicate comment detected");
    }

    [Fact]
    public async Task ProcessCommentAsync_UnmatchedComment_ReturnsNoMatchStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var incoming = new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "YouTube",
            PostId: "yt_video_001",
            CommentId: "yt_c_1",
            CommenterId: "viewer_10",
            CommenterUsername: "casual_watcher",
            CommentText: "Nice weather today, beautiful video backdrop!"
        );

        // Act
        var result = await _service.ProcessCommentAsync(incoming);

        // Assert
        result.Matched.Should().BeFalse();
        result.Status.Should().Be(CommentProcessingStatus.NoMatch);
        result.PublicReply.Should().BeNull();
        result.PrivateDm.Should().BeNull();
        result.CouponCode.Should().BeNull();
    }

    [Fact]
    public async Task FunnelAnalytics_And_ClickConversionTracking_AccuratelyCalculatesMetrics()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // 1. Process 3 comments (2 matches, 1 no-match)
        var c1 = await _service.ProcessCommentAsync(new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "Instagram",
            PostId: "p1",
            CommentId: "c1",
            CommenterId: "u1",
            CommenterUsername: "buyer1",
            CommentText: "PRICE please"
        ));

        var c2 = await _service.ProcessCommentAsync(new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "TikTok",
            PostId: "p2",
            CommentId: "c2",
            CommenterId: "u2",
            CommenterUsername: "buyer2",
            CommentText: "COUPON"
        ));

        var c3 = await _service.ProcessCommentAsync(new IncomingSocialCommentDto(
            TenantId: tenantId,
            Platform: "Facebook",
            PostId: "p3",
            CommentId: "c3",
            CommenterId: "u3",
            CommenterUsername: "buyer3",
            CommentText: "Hello there nice photo"
        ));

        // 2. Simulate Click on c1 and Conversion on c1
        c1.CouponCode.Should().NotBeNull();
        var clickSuccess = await _service.RecordClickAsync(tenantId, c1.CouponCode!);
        clickSuccess.Should().BeTrue();

        var convSuccess = await _service.RecordConversionAsync(tenantId, c1.CouponCode!, 149.99m);
        convSuccess.Should().BeTrue();

        // Act - Fetch Funnel Metrics
        var funnel = await _service.GetFunnelMetricsAsync(tenantId);

        // Assert
        funnel.Should().NotBeNull();
        funnel.TotalComments.Should().Be(3);
        funnel.MatchedIntent.Should().Be(2);
        funnel.DMsDelivered.Should().Be(2);
        funnel.LinkClicks.Should().Be(1);
        funnel.Conversions.Should().Be(1);
        funnel.TotalRevenue.Should().Be(149.99m);
        funnel.ClickThroughRatePercentage.Should().Be(50.0); // 1 click out of 2 DMs = 50%
        funnel.ConversionRatePercentage.Should().Be(50.0);   // 1 conv out of 2 DMs = 50%

        // Platform breakdown
        funnel.ByPlatform.Should().ContainKey("Instagram");
        funnel.ByPlatform["Instagram"].DmsSent.Should().Be(1);
        funnel.ByPlatform["Instagram"].Conversions.Should().Be(1);
        funnel.ByPlatform["Instagram"].Revenue.Should().Be(149.99m);
    }

    [Fact]
    public async Task CustomRules_CanBeCreated_Retrieved_AndDeleted()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customRule = new SocialCommentRule
        {
            TenantId = tenantId,
            Name = "Black Friday Midnight Blitz",
            Platform = SocialPlatform.TikTok,
            MatchType = SocialRuleMatchType.Contains,
            Keywords = new List<string> { "BLITZ", "MIDNIGHT", "50OFF" },
            DiscountPrefix = "BLITZ",
            DiscountPercent = 50,
            MinJitterSeconds = 5,
            MaxJitterSeconds = 15
        };

        // Act 1: Create
        var created = await _service.CreateOrUpdateRuleAsync(customRule);
        created.Should().NotBeNull();

        // Act 2: Retrieve
        var rules = await _service.GetRulesAsync(tenantId);
        rules.Should().Contain(r => r.Name == "Black Friday Midnight Blitz");

        // Act 3: Delete
        var deleted = await _service.DeleteRuleAsync(tenantId, created.Id);
        deleted.Should().BeTrue();

        var remainingRules = await _service.GetRulesAsync(tenantId);
        remainingRules.Should().NotContain(r => r.Id == created.Id);
    }

    [Fact]
    public void MapSocialCommentToDmEndpoints_RegistersAllRequiredRoutes()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        // Act
        app.MapSocialCommentToDmEndpoints();

        // Assert
        var endpointRouteBuilder = (IEndpointRouteBuilder)app;
        var endpoints = endpointRouteBuilder.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var routePatterns = endpoints.Select(e => e.RoutePattern.RawText).ToList();

        routePatterns.Should().Contain(r => r != null && r.Contains("api/v1/social/comment-to-dm/rules"));
        routePatterns.Should().Contain(r => r != null && r.Contains("api/v1/social/comment-to-dm/webhook"));
        routePatterns.Should().Contain(r => r != null && r.Contains("api/v1/social/comment-to-dm/logs"));
        routePatterns.Should().Contain(r => r != null && r.Contains("api/v1/social/comment-to-dm/analytics"));
    }
}
