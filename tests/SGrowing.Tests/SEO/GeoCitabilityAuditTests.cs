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
}
