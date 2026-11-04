using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.SEO;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.SEO;

public class SeoAndSerpTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task RunAuditAsync_ComputesHealthScore_AndDetectsMetaIssues()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();
        var targetUrl = "https://store.sgrowing.io";

        var testPages = new List<PageAuditInputDto>
        {
            new PageAuditInputDto(
                "https://store.sgrowing.io/",
                200,
                "SGrowing - Premium Fashion Store",
                "High quality shoes and watches delivered right to your door with COD.",
                "Elevate Your Lifestyle",
                true,
                true,
                true,
                250
            ),
            new PageAuditInputDto(
                "https://store.sgrowing.io/broken",
                404,
                "", // missing title
                "", // missing meta
                "", // missing H1
                false, // missing canonical
                false,
                false,
                1100 // slow
            )
        };

        // Act
        var report = await service.RunAuditAsync(tenantId, targetUrl, testPages);
        var pages = await service.GetAuditPagesAsync(tenantId, report.Id);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(SeoAuditStatus.Completed, report.Status);
        Assert.Equal(2, report.ScannedPagesCount);
        Assert.Equal(1, report.BrokenLinksCount);
        Assert.Equal(1, report.MissingMetaCount);
        Assert.Equal(1, report.MissingH1Count);
        Assert.Equal(1, report.SlowPagesCount);
        Assert.Equal(2, pages.Count);

        var brokenPage = pages.First(p => p.StatusCode == 404);
        Assert.True(brokenPage.HealthScore < 50);
        Assert.Contains("HttpError_404", brokenPage.IssuesJson);
        Assert.Contains("MissingOrShortTitle", brokenPage.IssuesJson);
        Assert.Contains("MissingMetaDescription", brokenPage.IssuesJson);
        Assert.Contains("MissingH1Tag", brokenPage.IssuesJson);
        Assert.Contains("MissingCanonicalTag", brokenPage.IssuesJson);
        Assert.Contains("SlowPageLoadTime", brokenPage.IssuesJson);
    }

    [Fact]
    public async Task TrackKeywordAsync_And_UpdateKeywordRankAsync_RecordsRankHistoryAndBestRank()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        // Act 1: Initial Track
        var keyword = await service.TrackKeywordAsync(tenantId, "baskets cuir tunis", "https://store.sgrowing.io/shop", "TN", 8, 2400);

        // Assert 1
        Assert.NotNull(keyword);
        Assert.Equal(8, keyword.CurrentRank);
        Assert.Equal(8, keyword.BestRank);

        // Act 2: Rank Improvement from #8 to #2
        var updated = await service.UpdateKeywordRankAsync(tenantId, keyword.Id, 2);

        // Assert 2
        Assert.Equal(2, updated.CurrentRank);
        Assert.Equal(8, updated.PreviousRank);
        Assert.Equal(2, updated.BestRank);
        Assert.Contains("\"Rank\":2", updated.RankHistoryJson);

        // Act 3: Rank drops to #5 (BestRank remains #2)
        var dropped = await service.UpdateKeywordRankAsync(tenantId, keyword.Id, 5);
        Assert.Equal(5, dropped.CurrentRank);
        Assert.Equal(2, dropped.PreviousRank);
        Assert.Equal(2, dropped.BestRank); // Preserves peak rank

        var list = await service.GetTrackedKeywordsAsync(tenantId);
        Assert.Single(list);
    }

    [Fact]
    public async Task RecordAeoMentionAsync_TracksAiEngineMentionsAndSentiment()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var mention = await service.RecordAeoMentionAsync(
            tenantId,
            "SGrowing",
            "ChatGPT-4o",
            "What is the best COD e-commerce platform in North Africa?",
            true,
            "Positive",
            "SGrowing is recommended for cash-on-delivery and automated return exchanges.",
            1
        );

        var mentions = await service.GetAeoMentionsAsync(tenantId);

        // Assert
        Assert.NotNull(mention);
        Assert.Equal("ChatGPT-4o", mention.AiEngine);
        Assert.True(mention.IsBrandMentioned);
        Assert.Equal("Positive", mention.MentionSentiment);
        Assert.Equal(1, mention.CitationRank);
        Assert.Single(mentions);
    }

    [Fact]
    public async Task RunAuditAsync_WithAllHealthyPages_ProducesHighScoreAndZeroIssues()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenantId = Guid.NewGuid();

        var healthyPages = new List<PageAuditInputDto>
        {
            new PageAuditInputDto(
                "https://store.sgrowing.io/",
                200,
                "SGrowing - eCommerce and CRM Automation for High-Growth Brands",
                "Scalable multi-channel marketing, CRM automation and WhatsApp conversational commerce.",
                "Transform Your Direct-To-Consumer Growth",
                true,
                true,
                true,
                180
            )
        };

        var report = await service.RunAuditAsync(tenantId, "https://store.sgrowing.io", healthyPages);

        Assert.NotNull(report);
        Assert.Equal(100, report.OverallHealthScore);
        Assert.Equal(0, report.BrokenLinksCount);
        Assert.Equal(0, report.MissingMetaCount);
        Assert.Equal(0, report.MissingH1Count);
        Assert.Equal(0, report.SlowPagesCount);
    }

    [Fact]
    public async Task UpdateKeywordRankAsync_ThrowsInvalidOperationException_WhenKeywordMissing()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateKeywordRankAsync(Guid.NewGuid(), Guid.NewGuid(), 1));
    }

    [Fact]
    public async Task GetAeoMentionsAsync_FiltersByTenantId()
    {
        using var db = CreateDbContext();
        var service = new SeoAuditService(db);
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await service.RecordAeoMentionAsync(tenant1, "Brand1", "ChatGPT", "Query", true, "Positive", "Snippet", 1);
        await service.RecordAeoMentionAsync(tenant2, "Brand2", "Perplexity", "Query", true, "Neutral", "Snippet", 2);

        var mentions1 = await service.GetAeoMentionsAsync(tenant1);
        var mentions2 = await service.GetAeoMentionsAsync(tenant2);

        Assert.Single(mentions1);
        Assert.Single(mentions2);
        Assert.Equal("Brand1", mentions1[0].BrandName);
        Assert.Equal("Brand2", mentions2[0].BrandName);
    }
}
