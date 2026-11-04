using System.Collections.Generic;
using SGrowing.Api.Modules.Content.Repurposing;
using Xunit;

namespace SGrowing.Tests.Content;

public class ContentRepurposingTests
{
    private readonly ContentRepurposingEngine _engine = new();

    [Fact]
    public void SeededAsset_ReturnsManifestWithDerivativesAndSlides()
    {
        var manifest = _engine.GetManifestByAssetId("asset_webinar_101");

        Assert.NotNull(manifest);
        Assert.Equal("The 2026 Shift from Manual SDR Outbound to Autonomous Agent Swarms", manifest.SourceTitle);
        Assert.NotEmpty(manifest.Derivatives);
        Assert.Contains(manifest.Derivatives, d => d.Channel == ContentChannel.SlideCarousel);

        var carousel = manifest.Derivatives.Find(d => d.Channel == ContentChannel.SlideCarousel);
        Assert.NotNull(carousel?.Slides);
        Assert.Equal(5, carousel.Slides.Count);
    }

    [Fact]
    public void DecomposeContent_GeneratesTargetChannels()
    {
        var request = new DecomposeContentRequest(
            "asset_new_tech",
            "Why We Built SGrowing",
            "Full engineering breakdown of our autonomous growth operating system...",
            new List<ContentChannel> { ContentChannel.LinkedInPost, ContentChannel.XTwitterThread }
        );

        var manifest = _engine.DecomposeContent(request);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Derivatives.Count);
        Assert.Contains(manifest.Derivatives, d => d.Channel == ContentChannel.LinkedInPost);
    }

    [Fact]
    public void GetDerivativeById_FindsExistingDerivative()
    {
        var derivative = _engine.GetDerivativeById("deriv_asset_webinar_101_li");

        Assert.NotNull(derivative);
        Assert.Equal(ContentChannel.LinkedInPost, derivative.Channel);
        Assert.NotEmpty(derivative.RecommendedHashtags);
    }

    [Fact]
    public void DecomposeContent_NullOrEmptyChannels_GeneratesFourDefaultChannels()
    {
        var request = new DecomposeContentRequest(
            "asset_default_test",
            "Autonomous Revenue",
            "Overview of autonomous pipeline execution",
            null!
        );

        var manifest = _engine.DecomposeContent(request);

        Assert.NotNull(manifest);
        Assert.Equal(4, manifest.Derivatives.Count);
        Assert.Contains(manifest.Derivatives, d => d.Channel == ContentChannel.LinkedInPost);
        Assert.Contains(manifest.Derivatives, d => d.Channel == ContentChannel.XTwitterThread);
        Assert.Contains(manifest.Derivatives, d => d.Channel == ContentChannel.ExecutiveNewsletter);
        Assert.Contains(manifest.Derivatives, d => d.Channel == ContentChannel.SlideCarousel);
    }

    [Fact]
    public void GetManifestByAssetId_NonExistent_ReturnsNull()
    {
        var manifest = _engine.GetManifestByAssetId("asset_does_not_exist_999");
        Assert.Null(manifest);
    }

    [Fact]
    public void GetDerivativeById_NonExistent_ReturnsNull()
    {
        var derivative = _engine.GetDerivativeById("deriv_non_existent_123");
        Assert.Null(derivative);
    }

    [Fact]
    public void DecomposeContent_WithShortVideoScript_GeneratesScriptWith60sReadingDuration()
    {
        var request = new DecomposeContentRequest(
            "asset_video_test",
            "Autonomous Agents in Practice",
            "Demo of agentic workflows",
            new List<ContentChannel> { ContentChannel.ShortVideoScript }
        );

        var manifest = _engine.DecomposeContent(request);

        Assert.NotNull(manifest);
        Assert.Single(manifest.Derivatives);
        var deriv = manifest.Derivatives[0];
        Assert.Equal(ContentChannel.ShortVideoScript, deriv.Channel);
        Assert.Equal(60, deriv.EstimatedReadSeconds);
        Assert.Contains("[Hook]", deriv.FormattedBody);
    }

    [Fact]
    public void DecomposeContent_UpdatesExistingManifest_WhenCalledWithSameSourceAssetId()
    {
        var assetId = "asset_repurpose_idempotent";
        var req1 = new DecomposeContentRequest(
            assetId,
            "Initial Title",
            "Initial body content",
            new List<ContentChannel> { ContentChannel.LinkedInPost }
        );
        var req2 = new DecomposeContentRequest(
            assetId,
            "Updated Title",
            "Updated body content with new data",
            new List<ContentChannel> { ContentChannel.LinkedInPost, ContentChannel.XTwitterThread }
        );

        var manifest1 = _engine.DecomposeContent(req1);
        Assert.Equal("Initial Title", manifest1.SourceTitle);
        Assert.Single(manifest1.Derivatives);

        var manifest2 = _engine.DecomposeContent(req2);
        Assert.Equal("Updated Title", manifest2.SourceTitle);
        Assert.Equal(2, manifest2.Derivatives.Count);

        var fetched = _engine.GetManifestByAssetId(assetId);
        Assert.NotNull(fetched);
        Assert.Equal("Updated Title", fetched.SourceTitle);
    }

    [Fact]
    public void DecomposeContent_KeyProofPoints_ContainsCoreMetrics()
    {
        var request = new DecomposeContentRequest(
            "asset_metrics_test",
            "Metrics Report",
            "Detailed report on conversion rate and sales pipeline",
            new List<ContentChannel> { ContentChannel.LinkedInPost }
        );

        var manifest = _engine.DecomposeContent(request);

        Assert.NotNull(manifest.KeyProofPoints);
        Assert.Equal(3, manifest.KeyProofPoints.Count);
        Assert.Contains(manifest.KeyProofPoints, t => t.Contains("8.4%"));
        Assert.Contains(manifest.KeyProofPoints, t => t.Contains("buying committee"));
    }

    [Fact]
    public void GetDerivativeById_CaseInsensitiveMatch_FindsDerivative()
    {
        var derivative = _engine.GetDerivativeById("DERIV_ASSET_WEBINAR_101_LI");

        Assert.NotNull(derivative);
        Assert.Equal(ContentChannel.LinkedInPost, derivative.Channel);
    }
}
