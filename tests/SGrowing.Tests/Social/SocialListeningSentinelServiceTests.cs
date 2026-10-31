using FluentAssertions;
using SGrowing.Api.Modules.Social.Listening;
using Xunit;

namespace SGrowing.Tests.Social;

public class SocialListeningSentinelServiceTests
{
    private readonly SocialListeningSentinelService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task IngestMentionAsync_ShouldClassifyPositiveSentiment_WhenPraiseKeywordsPresent()
    {
        var req = new IngestSocialMentionRequest(
            _tenantId,
            SocialPlatform.TwitterX,
            "@tech_enthusiast",
            1_200,
            "Love the new omnichannel WhatsApp feature on SGrowing, absolutely great and fast support!"
        );

        var mention = await _service.IngestMentionAsync(req);

        mention.Should().NotBeNull();
        mention.Sentiment.Should().Be(MentionSentiment.Positive);
        mention.SentimentScore.Should().BeGreaterThan(0.0);
        mention.ThreatLevel.Should().Be(CrisisThreatLevel.NormalGreen);
    }

    [Fact]
    public async Task IngestMentionAsync_ShouldTriggerCrisis_WhenSevereKeywordsDetected()
    {
        var req = new IngestSocialMentionRequest(
            _tenantId,
            SocialPlatform.Trustpilot,
            "angry_shopper",
            500,
            "This company is a scam and fraud! Never received my order and support is a thief."
        );

        var mention = await _service.IngestMentionAsync(req);

        mention.Sentiment.Should().Be(MentionSentiment.SevereHostile);
        mention.SentimentScore.Should().BeLessThan(-0.80);
        mention.DetectedCrisisKeywords.Should().Contain("scam");
        mention.DetectedCrisisKeywords.Should().Contain("fraud");

        // Verify crisis incident was auto-created and broadcasts paused
        var incidents = await _service.GetActiveCrisisIncidentsAsync(_tenantId);
        incidents.Should().NotBeEmpty();
        incidents[0].IsBroadcastPaused.Should().BeTrue();

        var summary = await _service.GetSocialListeningSummaryAsync(_tenantId);
        summary.IsMarketingBroadcastHalted.Should().BeTrue();
    }

    [Fact]
    public async Task IngestMentionAsync_ShouldElevateThreatToEmergency_WhenHighInfluenceAuthorPostsNegative()
    {
        var req = new IngestSocialMentionRequest(
            _tenantId,
            SocialPlatform.TwitterX,
            "@famous_influencer",
            120_000, // > 50k followers
            "Total catastrophe with this service today, broken delivery and unhelpful agents."
        );

        var mention = await _service.IngestMentionAsync(req);

        mention.ThreatLevel.Should().Be(CrisisThreatLevel.CrisisRedEmergency);
    }

    [Fact]
    public async Task ResolveCrisisIncidentAsync_ShouldLiftBroadcastHalt_WhenAllIncidentsResolved()
    {
        var req = new IngestSocialMentionRequest(
            _tenantId, SocialPlatform.TwitterX, "@user1", 100, "Major scam alert on this platform!"
        );
        await _service.IngestMentionAsync(req);

        var activeIncidents = await _service.GetActiveCrisisIncidentsAsync(_tenantId);
        activeIncidents.Should().ContainSingle();

        var resolved = await _service.ResolveCrisisIncidentAsync(new ResolveCrisisIncidentRequest(
            _tenantId, activeIncidents[0].IncidentId, "Issue was a courier misrouting, refunded customer and issued public apology."
        ));

        resolved.Should().NotBeNull();
        resolved!.ResolvedAtUtc.Should().NotBeNull();

        // Broadcast halt should now be lifted
        var summary = await _service.GetSocialListeningSummaryAsync(_tenantId);
        summary.IsMarketingBroadcastHalted.Should().BeFalse();
        summary.ActiveCrisisCount.Should().Be(0);
    }

    [Fact]
    public async Task ListMentionsAsync_ShouldFilterByPlatformAndSentiment()
    {
        await _service.IngestMentionAsync(new IngestSocialMentionRequest(_tenantId, SocialPlatform.LinkedIn, "@lead", 200, "Great experience love it"));
        await _service.IngestMentionAsync(new IngestSocialMentionRequest(_tenantId, SocialPlatform.GoogleReviews, "@reviewer", 50, "Delayed shipment"));

        var linkedInOnly = await _service.ListMentionsAsync(_tenantId, platform: SocialPlatform.LinkedIn);
        linkedInOnly.Should().ContainSingle(m => m.Platform == SocialPlatform.LinkedIn);

        var negativeOnly = await _service.ListMentionsAsync(_tenantId, sentiment: MentionSentiment.Negative);
        negativeOnly.Should().ContainSingle(m => m.Platform == SocialPlatform.GoogleReviews);
    }

    [Fact]
    public async Task GetSentimentTrendsAsync_ShouldReturnHourlyAggregates()
    {
        await _service.IngestMentionAsync(new IngestSocialMentionRequest(_tenantId, SocialPlatform.TwitterX, "@a", 10, "Great love"));
        await _service.IngestMentionAsync(new IngestSocialMentionRequest(_tenantId, SocialPlatform.TwitterX, "@b", 10, "Broken bad"));

        var trends = await _service.GetSentimentTrendsAsync(_tenantId, hours: 12);

        trends.Should().HaveCount(12);
        trends.Sum(t => t.PositiveCount).Should().Be(1);
        trends.Sum(t => t.NegativeCount).Should().Be(1);
    }
}
