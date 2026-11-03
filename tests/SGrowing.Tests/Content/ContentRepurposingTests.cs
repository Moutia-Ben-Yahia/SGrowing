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
}
