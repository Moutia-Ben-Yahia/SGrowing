using SGrowing.Api.Modules.SEO.GeoVisibility;

namespace SGrowing.Tests.SEO;

public class GeoVisibilityTests
{
    private readonly GeoVisibilityEngine _engine = new();

    [Fact]
    public void GetSummary_ReturnsScoredGeoVisibilityMetrics()
    {
        var summary = _engine.GetSummary("tenant_test", "SGrowing");

        Assert.NotNull(summary);
        Assert.Equal("SGrowing", summary.BrandName);
        Assert.True(summary.OverallGeoScore >= 80);
        Assert.True(summary.ShareOfModelVoicePercent > 40.0);
        Assert.NotEmpty(summary.EngineBreakdown);
        Assert.NotEmpty(summary.StrategicRecommendations);
    }

    [Fact]
    public void ListCitations_ReturnsSeededAnswerEngineCitations()
    {
        var citations = _engine.ListCitations("tenant_test", "SGrowing");

        Assert.NotEmpty(citations);
        Assert.Contains(citations, c => c.Engine == AiAnswerEngine.Perplexity);
        Assert.Contains(citations, c => c.Engine == AiAnswerEngine.ChatGptSearch);
        Assert.Contains(citations, c => c.Engine == AiAnswerEngine.GoogleAiOverviews);
    }

    [Fact]
    public void AuditQuery_DispatchesAcrossTargetEngines()
    {
        var req = new AuditQueryRequest(
            TenantId: "tenant_test",
            BrandName: "SGrowing",
            TargetQuery: "Unified marketing CRM with autonomous AI swarm",
            Engines: new List<AiAnswerEngine> { AiAnswerEngine.Perplexity, AiAnswerEngine.ChatGptSearch }
        );

        var audited = _engine.AuditQuery(req);

        Assert.Equal(2, audited.Count);
        Assert.All(audited, c => Assert.Equal("SGrowing", c.BrandMentioned));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetSummary_WithEmptyBrand_DefaultsToSGrowing(string? brand)
    {
        var summary = _engine.GetSummary("tenant_test_empty", brand!);

        Assert.Equal("SGrowing", summary.BrandName);
        Assert.True(summary.TotalCitationsTracked >= 4);
    }

    [Fact]
    public void ListCitations_WithBrandFilter_ReturnsOnlyMatchingBrand()
    {
        var brandCitations = _engine.ListCitations("tenant_test", "SGrowing");
        Assert.All(brandCitations, c => Assert.Equal("SGrowing", c.BrandMentioned));

        var nonExistent = _engine.ListCitations("tenant_test", "NonExistentBrandXYZ");
        Assert.Empty(nonExistent);
    }

    [Fact]
    public void AuditQuery_WithEmptyEnginesList_DefaultsToThreeMajorEngines()
    {
        var req = new AuditQueryRequest(
            TenantId: "tenant_defaults",
            BrandName: "BrandX",
            TargetQuery: "Automated logistics tracking",
            Engines: new List<AiAnswerEngine>() // empty -> defaults to Perplexity, ChatGptSearch, GoogleAiOverviews
        );

        var audited = _engine.AuditQuery(req);

        Assert.Equal(3, audited.Count);
        Assert.Contains(audited, c => c.Engine == AiAnswerEngine.Perplexity);
        Assert.Contains(audited, c => c.Engine == AiAnswerEngine.ChatGptSearch);
        Assert.Contains(audited, c => c.Engine == AiAnswerEngine.GoogleAiOverviews);

        // Verify they are searchable in ListCitations
        var allBrandX = _engine.ListCitations("tenant_defaults", "BrandX");
        Assert.Equal(3, allBrandX.Count);
    }
}
