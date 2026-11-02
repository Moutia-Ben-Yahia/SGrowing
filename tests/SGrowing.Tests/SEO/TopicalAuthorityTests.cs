using System.Collections.Generic;
using SGrowing.Api.Modules.SEO.TopicalAuthority;
using Xunit;

namespace SGrowing.Tests.SEO;

public class TopicalAuthorityTests
{
    private readonly TopicalAuthorityEngine _engine = new();

    [Fact]
    public void AnalyzeTopicalMesh_ValidDomain_ReturnsClusterScoresAndGaps()
    {
        // Arrange
        var request = new AnalyzeTopicalMeshRequest(
            DomainOrBaseUrl: "sgrowing.io",
            ExistingArticleUrls: new List<string> { "/blog/post-1", "/blog/post-2" }
        );

        // Act
        var result = _engine.AnalyzeTopicalMesh(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("sgrowing.io", result.Domain);
        Assert.True(result.OverallTopicalAuthorityScore > 70.0);
        Assert.True(result.TotalClustersAudited >= 2);
        Assert.NotEmpty(result.Clusters);
    }

    [Fact]
    public void GetInternalLinkOpportunities_ReturnsPrioritizedAnchorRecommendations()
    {
        // Act
        var opportunities = _engine.GetInternalLinkOpportunities();

        // Assert
        Assert.NotNull(opportunities);
        Assert.NotEmpty(opportunities);
        Assert.All(opportunities, opp =>
        {
            Assert.False(string.IsNullOrWhiteSpace(opp.SourcePageUrl));
            Assert.False(string.IsNullOrWhiteSpace(opp.TargetPageUrl));
            Assert.False(string.IsNullOrWhiteSpace(opp.RecommendedAnchorText));
            Assert.True(opp.PredictedRelevanceScore >= 0.85);
        });
    }

    [Fact]
    public void ApplyLinkInjection_ValidRequest_SynthesizesAnchorTag()
    {
        // Arrange
        var request = new ApplyLinkInjectionRequest(
            OpportunityId: "opp_test_01",
            SourcePageUrl: "/blog/scale-outbound",
            TargetPageUrl: "/solutions/b2b-growth-automation",
            AnchorText: "autonomous B2B growth automation"
        );

        // Act
        var result = _engine.ApplyLinkInjection(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Applied);
        Assert.Contains("href=\"/solutions/b2b-growth-automation\"", result.InjectedHtmlTag);
        Assert.Contains("autonomous B2B growth automation", result.InjectedHtmlTag);
    }
}
