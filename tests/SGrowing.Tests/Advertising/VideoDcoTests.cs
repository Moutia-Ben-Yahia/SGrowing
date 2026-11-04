using System;
using System.Linq;
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
    public void GenerateManifest_With_Vertical_Template_Produces_916_Frames()
    {
        var request = new GenerateVideoManifestRequest(
            "TPL-VERTICAL-916",
            "Gen-Z Summer Drop",
            "TND",
            89m,
            "Offre Spéciale Aujourd'hui",
            "Swipe Up to Buy",
            "https://cdn.sgrowing.io/logos/brand-summer.svg"
        );

        var manifest = _engine.GenerateManifest(request);

        manifest.Should().NotBeNull();
        manifest.AspectRatio.Should().Be("9:16");
        manifest.EstimatedRenderFrames.Should().Be(900); // 15s * 60fps
        manifest.LayerSequences.Should().Contain("SwipeUpCTA");
        manifest.OverlayJson.Should().Contain("89");
    }

    [Fact]
    public void GenerateManifest_With_Square_Template_Produces_11_Frames()
    {
        var request = new GenerateVideoManifestRequest(
            "TPL-SQUARE-11",
            "B2B SaaS Growth Carousel",
            "EUR",
            1200m,
            "Scale revenue with zero dev ops",
            "Schedule Strategy Call",
            "https://cdn.sgrowing.io/logos/brand-b2b.svg"
        );

        var manifest = _engine.GenerateManifest(request);

        manifest.Should().NotBeNull();
        manifest.AspectRatio.Should().Be("1:1");
        manifest.EstimatedRenderFrames.Should().Be(1200); // 20s * 60fps
        manifest.LayerSequences.Should().Contain("BookDemoButton");
    }

    [Fact]
    public void GenerateManifest_Falls_Back_To_First_Template_When_Unknown()
    {
        var request = new GenerateVideoManifestRequest(
            "UNKNOWN-TPL-ID",
            "Fallback Test",
            "USD",
            100m,
            "Headline",
            "CTA",
            ""
        );

        var manifest = _engine.GenerateManifest(request);

        manifest.Should().NotBeNull();
        manifest.TemplateId.Should().NotBeNullOrEmpty();
        manifest.EstimatedRenderFrames.Should().BeGreaterThan(0);
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
        receipt.CdnPurgeReceipt.Should().StartWith("PURGE-");
        receipt.RenderDurationMs.Should().Be(1420);
        receipt.Status.Should().Be("RENDER_COMPLETE");
        receipt.OutputVideoUrl.Should().Contain("-4k.mp4");
    }

    [Fact]
    public void RenderVariant_Without_Cache_Invalidation_Sets_No_Purge()
    {
        var request = new RenderVideoVariantRequest(
            "MAN-DEMO-5678",
            "1080P",
            InvalidateCdnCache: false
        );

        var receipt = _engine.RenderVariant(request);

        receipt.Should().NotBeNull();
        receipt.CdnPurgeReceipt.Should().Be("NO_PURGE");
        receipt.OutputVideoUrl.Should().Contain("-1080p.mp4");
    }

    [Fact]
    public void GenerateManifest_ProducesUniqueManifestIds_AcrossMultipleCalls()
    {
        var req1 = new GenerateVideoManifestRequest(
            "TPL-LANDSCAPE-169", "Target 1", "USD", 500m, "Headline 1", "CTA 1", "https://cdn.example.com/logo1.svg");
        var req2 = new GenerateVideoManifestRequest(
            "TPL-LANDSCAPE-169", "Target 2", "EUR", 600m, "Headline 2", "CTA 2", "https://cdn.example.com/logo2.svg");

        var manifest1 = _engine.GenerateManifest(req1);
        var manifest2 = _engine.GenerateManifest(req2);

        manifest1.ManifestId.Should().StartWith("MAN-");
        manifest2.ManifestId.Should().StartWith("MAN-");
        manifest1.ManifestId.Should().NotBe(manifest2.ManifestId);
    }

    [Fact]
    public void GenerateManifest_PreservesAudioTrackUrl_AcrossAllTemplates()
    {
        var templates = new[] { "TPL-LANDSCAPE-169", "TPL-VERTICAL-916", "TPL-SQUARE-11" };

        foreach (var tplId in templates)
        {
            var manifest = _engine.GenerateManifest(new GenerateVideoManifestRequest(
                tplId, "Global Audience", "USD", 99m, "Headline", "CTA", ""));

            manifest.AudioTrackUrl.Should().Be("https://cdn.sgrowing.io/audio/ambient-tech-pulse-48k.aac");
        }
    }

    [Fact]
    public void GenerateManifest_WithZeroPrice_FormatsOverlayJsonCorrectly()
    {
        var manifest = _engine.GenerateManifest(new GenerateVideoManifestRequest(
            "TPL-LANDSCAPE-169", "Free Trial Segment", "USD", 0m, "Start Free Today", "Get Started Free", ""));

        manifest.OverlayJson.Should().Contain("\"price\": 0");
        manifest.OverlayJson.Should().Contain("\"headline\": \"Start Free Today\"");
        manifest.OverlayJson.Should().Contain("\"cta\": \"Get Started Free\"");
    }

    [Theory]
    [InlineData("720p", "-720p.mp4")]
    [InlineData("1080P", "-1080p.mp4")]
    [InlineData("4K", "-4k.mp4")]
    [InlineData("8k", "-8k.mp4")]
    public void RenderVariant_SupportsMultipleResolutions_WithNormalizedUrl(string resolution, string expectedSuffix)
    {
        var receipt = _engine.RenderVariant(new RenderVideoVariantRequest("MAN-TEST-RES", resolution, InvalidateCdnCache: true));

        receipt.OutputResolution.Should().Be(resolution);
        receipt.OutputVideoUrl.Should().EndWith(expectedSuffix);
        receipt.CdnPurgeReceipt.Should().StartWith("PURGE-");
        receipt.Status.Should().Be("RENDER_COMPLETE");
    }

    [Fact]
    public void RenderVariant_ProducesUniqueJobIds_ForSuccessiveRenders()
    {
        var r1 = _engine.RenderVariant(new RenderVideoVariantRequest("MAN-1", "1080p", false));
        var r2 = _engine.RenderVariant(new RenderVideoVariantRequest("MAN-2", "1080p", false));

        r1.JobId.Should().StartWith("JOB-");
        r2.JobId.Should().StartWith("JOB-");
        r1.JobId.Should().NotBe(r2.JobId);
    }

    [Fact]
    public void GetOverview_Templates_AllHaveActiveStatusAndNonEmptyDynamicLayers()
    {
        var overview = _engine.GetOverview();

        overview.Templates.Should().NotBeEmpty();
        foreach (var tpl in overview.Templates)
        {
            tpl.TemplateId.Should().StartWith("TPL-");
            tpl.Status.Should().Be("ACTIVE");
            tpl.DurationSeconds.Should().BeGreaterThan(0);
            tpl.DynamicLayers.Should().NotBeEmpty();
            tpl.DynamicLayers.Count.Should().BeGreaterThanOrEqualTo(3);
        }
    }
}
