using FluentAssertions;
using SGrowing.Api.Modules.Advertising.VideoDco;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class VideoDcoTests
{
    private readonly VideoDcoEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsTemplatesAndVariants()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveTemplatesCount.Should().Be(3);
        overview.TotalRenderedVariantsCount.Should().Be(2);
        overview.CdnCacheHitRatioPct.Should().BeGreaterThan(90.0);
        overview.Templates.Should().HaveCount(3);
        overview.RecentVariants.Should().HaveCount(2);
    }

    [Fact]
    public void GenerateManifest_ProducesValidVideoLayersAndFrameCount()
    {
        var request = new GenerateVideoManifestRequest(
            "TPL-LANDSCAPE-169",
            "APAC FinTech Leaders",
            "SGD",
            7500m,
            "Unlock Autonomous Scale",
            "Book VIP Executive Briefing",
            "https://cdn.sgrowing.io/logos/brand-gold.svg"
        );

        var manifest = _engine.GenerateManifest(request);

        manifest.Should().NotBeNull();
        manifest.ManifestId.Should().StartWith("MAN-");
        manifest.AspectRatio.Should().Be("16:9");
        manifest.EstimatedRenderFrames.Should().Be(1800); // 30s * 60fps
        manifest.LayerSequences.Should().Contain("DynamicPriceBadge");
        manifest.OverlayJson.Should().Contain("SGD");
    }

    [Fact]
    public void RenderVariant_DispatchesJobAndEmitsReceipt()
    {
        var request = new RenderVideoVariantRequest(
            "MAN-DEMO-1234",
            "4K",
            true
        );

        var receipt = _engine.RenderVariant(request);

        receipt.Should().NotBeNull();
        receipt.JobId.Should().StartWith("JOB-");
        receipt.OutputResolution.Should().Be("4K");
        receipt.Status.Should().Be("RENDER_COMPLETE");
        receipt.OutputVideoUrl.Should().Contain("4k.mp4");
        receipt.CdnPurgeReceipt.Should().StartWith("PURGE-");
    }
}
