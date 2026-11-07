using FluentAssertions;
using SGrowing.Api.Modules.SEO.GeoAeoRadar;
using Xunit;

namespace SGrowing.Tests.Unit;

public class GeoAeoRadarTests
{
    private readonly GeoAeoRadarService _service = new();

    [Fact]
    public void Constructor_DefaultTenantIsSeededWithInitialQueriesAndAudits()
    {
        // Act
        var queries = _service.GetQueries(GeoAeoRadarService.DefaultTenantId);
        var manifest = _service.GetCrawlerManifest(GeoAeoRadarService.DefaultTenantId);
        var report = _service.GetOverviewReport(GeoAeoRadarService.DefaultTenantId);

        // Assert
        queries.Should().NotBeEmpty();
        queries.Count.Should().BeGreaterThanOrEqualTo(4);
        manifest.Should().NotBeNull();
        manifest.Crawlers.Should().Contain(c => c.BotName == "GPTBot");
        manifest.Crawlers.Should().Contain(c => c.BotName == "PerplexityBot");
        manifest.Crawlers.Should().Contain(c => c.BotName == "ClaudeBot");
        manifest.Crawlers.Should().Contain(c => c.BotName == "Google-Extended");
        manifest.OverallRobotsStatus.Should().Be(CrawlerReadinessStatus.Compliant);
        manifest.LlmsTxtStatus.Should().Be(CrawlerReadinessStatus.Compliant);

        report.Should().NotBeNull();
        report.TotalQueriesTracked.Should().Be(queries.Count);
        report.ShareOfModelVoicePercent.Should().BeGreaterThan(50.0);
        report.HighCitabilityAuditsCount.Should().BeGreaterThan(0);
        report.EngineBreakdown.Should().HaveCount(4);
    }

    [Fact]
    public void StrictTenantIsolation_IsolatedTenantHasNoPreseededQueriesOrAudits()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();

        // Act
        var queries = _service.GetQueries(otherTenantId);
        var report = _service.GetOverviewReport(otherTenantId);

        // Assert
        queries.Should().BeEmpty();
        report.TotalQueriesTracked.Should().Be(0);
        report.TotalCitationsCount.Should().Be(0);
        report.HighCitabilityAuditsCount.Should().Be(0);
        report.TenantId.Should().Be(otherTenantId);
    }

    [Fact]
    public void TrackQuery_WithValidParameters_InsertsRecordAndCalculatesShareOfVoice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new AddCitationQueryRequest(
            Query: "autonomous omnichannel marketing orchestrator with clickhouse analytics",
            Engine: AiEngineType.Perplexity,
            CitedUrl: "https://sgrowing.io/solutions/omnichannel",
            CitationRank: 1,
            Sentiment: "Positive"
        );

        // Act
        var record = _service.TrackQuery(tenantId, request);
        var queries = _service.GetQueries(tenantId);

        // Assert
        record.Should().NotBeNull();
        record.TenantId.Should().Be(tenantId);
        record.Query.Should().Be("autonomous omnichannel marketing orchestrator with clickhouse analytics");
        record.Engine.Should().Be(AiEngineType.Perplexity);
        record.CitationRank.Should().Be(1);
        record.ShareOfVoice.Should().BeGreaterThanOrEqualTo(80.0);
        record.Sentiment.Should().Be("Positive");

        queries.Should().ContainSingle();
        queries[0].Id.Should().Be(record.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TrackQuery_WithEmptyQuery_ThrowsArgumentException(string invalidQuery)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new AddCitationQueryRequest(Query: invalidQuery);

        // Act
        var act = () => _service.TrackQuery(tenantId, request);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*query cannot be empty*");
    }

    [Fact]
    public void GetQueries_ReturnsAllTrackedQueriesForTenantInDescendingOrder()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _service.TrackQuery(tenantId, new AddCitationQueryRequest("query-1", AiEngineType.Perplexity, CitationRank: 1));
        _service.TrackQuery(tenantId, new AddCitationQueryRequest("query-2", AiEngineType.ChatGPT_Search, CitationRank: 2));

        // Act
        var queries = _service.GetQueries(tenantId);

        // Assert
        queries.Should().HaveCount(2);
        queries[0].Query.Should().Be("query-2"); // latest inserted first
        queries[1].Query.Should().Be("query-1");
    }

    [Fact]
    public void AuditPageCitability_WithHighQualityStructuredMarkdown_ReturnsHighCitabilityGrade()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var content = """
        # SGrowing Autonomous Growth Operating System

        SGrowing provides high-velocity enterprise growth automation, integrating real-time marketing attribution, CRM routing, and automated Cash on Delivery courier settlement.

        ## What is AI Engine Optimization (GEO)?
        Generative Engine Optimization is the discipline of structuring primary data and technical capabilities to maximize authoritative citations by Perplexity, ChatGPT Search, and Gemini.

        ## Verified Architecture Benchmarks
        - 10,000 EPS partitioned event streaming with sub-second OLAP projection.
        - ISO 32000-1 PAdES-B-LTV digital contract signing vault.
        - Multi-tenant PostgreSQL 17 with kernel-level Row Level Security.

        | Module | P99 Latency | Availability SLA |
        |---|---|---|
        | Event Ingestion | < 15ms | 99.99% |
        | ClickHouse OLAP | < 120ms | 99.95% |
        """;

        var request = new AuditPageCitabilityRequest(
            Url: "https://sgrowing.io/architecture",
            Title: "SGrowing Architecture Overview",
            Content: content,
            TargetEngine: AiEngineType.Perplexity
        );

        // Act
        var audit = _service.AuditPageCitability(tenantId, request);

        // Assert
        audit.Should().NotBeNull();
        audit.OverallScore.Should().BeGreaterThanOrEqualTo(75.0);
        audit.Grade.Should().Be(CitabilityGrade.High);
        audit.StructuredFormatRatio.Should().BeGreaterThanOrEqualTo(50.0);
        audit.FactualClaimsCount.Should().BeGreaterThan(0);
        audit.AmbiguousPronounOccurrences.Should().Be(0);
        audit.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public void AuditPageCitability_WithVaguePronounsAndNoStructure_ReturnsNeedsImprovementWithRecommendations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var vagueContent = """
        It is very nice and it does lots of great things for people who want it.
        When they use this, it helps them because they can do things better without having to think about it.
        They really like it and it makes them happy every single time they try to use it.
        """;

        var request = new AuditPageCitabilityRequest(
            Url: "https://example.com/vague-page",
            Title: "Vague Description",
            Content: vagueContent
        );

        // Act
        var audit = _service.AuditPageCitability(tenantId, request);

        // Assert
        audit.Should().NotBeNull();
        audit.OverallScore.Should().BeLessThan(55.0);
        audit.Grade.Should().BeOneOf(CitabilityGrade.NeedsImprovement, CitabilityGrade.Moderate);
        audit.AmbiguousPronounOccurrences.Should().BeGreaterThan(3);
        audit.Recommendations.Should().Contain(r => r.Contains("ambiguous pronouns", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("", "Valid Title", "Valid Content")]
    [InlineData("https://example.com", "Valid Title", "")]
    public void AuditPageCitability_WithMissingUrlOrContent_ThrowsArgumentException(string url, string title, string content)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new AuditPageCitabilityRequest(url, title, content);

        // Act
        var act = () => _service.AuditPageCitability(tenantId, request);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetCrawlerManifest_ReturnsCompliantStatusForAgenticCrawlers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var manifest = _service.GetCrawlerManifest(tenantId);

        // Assert
        manifest.Should().NotBeNull();
        manifest.TenantId.Should().Be(tenantId);
        manifest.Crawlers.Should().HaveCount(4);
        manifest.Crawlers.Should().OnlyContain(c => c.Status == CrawlerReadinessStatus.Compliant);
        manifest.Crawlers.Should().OnlyContain(c => c.AllowedInRobotsTxt);
        manifest.ActionItems.Should().NotBeEmpty();
    }

    [Fact]
    public void GetOverviewReport_AggregatesEngineBreakdownAndCalculatesShareOfModelVoice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _service.TrackQuery(tenantId, new AddCitationQueryRequest("query A", AiEngineType.Perplexity, CitationRank: 1));
        _service.TrackQuery(tenantId, new AddCitationQueryRequest("query B", AiEngineType.ChatGPT_Search, CitationRank: 2));

        // Act
        var report = _service.GetOverviewReport(tenantId);

        // Assert
        report.Should().NotBeNull();
        report.TotalQueriesTracked.Should().Be(2);
        report.TotalCitationsCount.Should().Be(2);
        report.ShareOfModelVoicePercent.Should().BeGreaterThan(60.0);
        report.EngineBreakdown.Should().HaveCount(4);

        var perplexityStats = report.EngineBreakdown.First(e => e.Engine == AiEngineType.Perplexity);
        perplexityStats.TotalCitations.Should().Be(1);
        perplexityStats.AverageRank.Should().Be(1.0);

        var chatGptStats = report.EngineBreakdown.First(e => e.Engine == AiEngineType.ChatGPT_Search);
        chatGptStats.TotalCitations.Should().Be(1);
        chatGptStats.AverageRank.Should().Be(2.0);
    }
}
