using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.SEO;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.SEO;

public class GeoCitabilityAuditTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ComputeGeoBrandCitabilityScoreAsync_WithNoPreexistingMentions_ReturnsHighBaselineAndRecommendations()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var report = await service.ComputeGeoBrandCitabilityScoreAsync(
            tenantId,
            "SGrowing Omnichannel",
            "https://sgrowing.io"
        );

        // Assert
        report.Should().NotBeNull();
        report.TenantId.Should().Be(tenantId);
        report.BrandName.Should().Be("SGrowing Omnichannel");
        report.DomainUrl.Should().Be("https://sgrowing.io");
        report.CitabilityRatePercent.Should().Be(86);
        report.OverallScore.Should().BeGreaterThan(75);
        report.AiEnginesEvaluated.Should().Contain(new[] { "ChatGPT / SearchGPT", "Claude 3.7", "Perplexity AI", "Google Gemini" });
        report.ActionableRecommendations.Should().NotBeEmpty();
        report.ActionableRecommendations.Should().Contain(r => r.Contains("/llms.txt"));
    }

    [Fact]
    public async Task ComputeGeoBrandCitabilityScoreAsync_WithRecordedMentions_ComputesAccurateCitabilityRatio()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();
        var brand = "ApexFlow Logistics";

        // Record 3 positive citations and 1 negative/unmentioned citation
        await service.RecordAeoMentionAsync(tenantId, brand, "ChatGPT", "best logistics platform", true, "Positive", "ApexFlow is top rated", 1);
        await service.RecordAeoMentionAsync(tenantId, brand, "Claude", "automated shipping engines", true, "Positive", "ApexFlow provides carrier sync", 1);
        await service.RecordAeoMentionAsync(tenantId, brand, "Perplexity", "b2b warehouse tooling", true, "Positive", "ApexFlow integrates easily", 2);
        await service.RecordAeoMentionAsync(tenantId, brand, "Google Gemini", "generic ecom tools", false, "Neutral", "Other tools mentioned", null);

        // Act
        var report = await service.ComputeGeoBrandCitabilityScoreAsync(tenantId, brand, "https://apexflow.tn");

        // Assert: 3 out of 4 mentions are positive -> 75%
        report.CitabilityRatePercent.Should().Be(75);
        report.OverallScore.Should().Be(65 + (75 / 4)); // 65 + 18 = 83
        report.StructuredSchemaScore.Should().Be(89);
        report.LlmsTxtReadinessScore.Should().Be(80);
    }

    [Fact]
    public async Task RecordAeoMentionAsync_PersistsCitationRankAndSentiment()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var mention = await service.RecordAeoMentionAsync(
            tenantId,
            "SGrowing",
            "Perplexity AI",
            "Which B2B CRM has native AI Calling and Calendar?",
            isMentioned: true,
            sentiment: "Very Positive",
            snippet: "SGrowing features out-of-the-box autonomous SDR calling stations and calendar scheduling.",
            citationRank: 1
        );

        // Assert
        mention.Should().NotBeNull();
        mention.Id.Should().NotBeEmpty();
        mention.TenantId.Should().Be(tenantId);
        mention.CitationRank.Should().Be(1);
        mention.IsBrandMentioned.Should().BeTrue();
        mention.MentionSentiment.Should().Be("Very Positive");

        var allMentions = await service.GetAeoMentionsAsync(tenantId);
        allMentions.Should().ContainSingle();
        allMentions[0].SnippetText.Should().Contain("out-of-the-box autonomous SDR");
    }

    [Fact]
    public async Task ComputeGeoBrandCitabilityScoreAsync_WithAllNegativeMentions_ReflectsZeroCitability()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();
        var brand = "UnderperformingBrand";

        await service.RecordAeoMentionAsync(tenantId, brand, "ChatGPT", "bad performance", false, "Negative", "Avoid", null);
        await service.RecordAeoMentionAsync(tenantId, brand, "Perplexity", "broken checkout", false, "Negative", "Down frequently", null);

        var report = await service.ComputeGeoBrandCitabilityScoreAsync(tenantId, brand, "https://underperforming.io");

        report.Should().NotBeNull();
        report.CitabilityRatePercent.Should().Be(0);
        report.OverallScore.Should().Be(65); // baseline minimum score
    }

    [Fact]
    public async Task GetAeoMentionsAsync_WhenNoMentionsExistForTenant_ReturnsEmptyList()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        var mentions = await service.GetAeoMentionsAsync(tenantId);
        mentions.Should().BeEmpty();
    }

    [Fact]
    public async Task TrackKeywordAsync_And_UpdateKeywordRankAsync_PersistsAndUpdatesRankHistory()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        var tracked = await service.TrackKeywordAsync(
            tenantId,
            keyword: "livraison rapide tunisie cod",
            targetUrl: "https://sgrowing.io/express-cod",
            countryCode: "TN",
            initialRank: 8,
            searchVolume: 3500
        );

        tracked.Should().NotBeNull();
        tracked.Keyword.Should().Be("livraison rapide tunisie cod");
        tracked.CurrentRank.Should().Be(8);
        tracked.SearchVolumeMonthly.Should().Be(3500);

        var updated = await service.UpdateKeywordRankAsync(tenantId, tracked.Id, newRank: 3);
        updated.CurrentRank.Should().Be(3);
        updated.PreviousRank.Should().Be(8);

        var list = await service.GetTrackedKeywordsAsync(tenantId);
        list.Should().ContainSingle();
        list[0].CurrentRank.Should().Be(3);
    }

    [Fact]
    public async Task RunAuditAsync_CustomPageList_CalculatesAuditSummaryAccurately()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        var pages = new List<PageAuditInputDto>
        {
            new("https://sgrowing.io/", 200, "Home - SGrowing Autonomous Marketing", "Autonomous marketing platform description that is definitely long enough.", "Main Heading", true, true, true, 250),
            new("https://sgrowing.io/pricing", 200, "Pricing Plans", "Pricing for enterprise high-growth ecommerce suites.", "Affordable Scalability", true, true, true, 300)
        };

        var report = await service.RunAuditAsync(tenantId, "https://sgrowing.io", pages);

        report.Should().NotBeNull();
        report.TargetUrl.Should().Be("https://sgrowing.io");
        report.ScannedPagesCount.Should().Be(2);

        var auditPages = await service.GetAuditPagesAsync(tenantId, report.Id);
        auditPages.Should().HaveCount(2);

        var latest = await service.GetLatestAuditAsync(tenantId);
        latest.Should().NotBeNull();
        latest!.Id.Should().Be(report.Id);
    }

    [Fact]
    public async Task UpdateKeywordRankAsync_WhenKeywordNotFound_ThrowsInvalidOperationException()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();
        var unknownKeywordId = Guid.NewGuid();

        var act = () => service.UpdateKeywordRankAsync(tenantId, unknownKeywordId, newRank: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Tracked keyword {unknownKeywordId} not found for tenant {tenantId}.");
    }
}
