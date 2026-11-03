using SGrowing.Api.Modules.Advertising.AudioDai;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class AudioDaiTests
{
    [Fact]
    public void GetOverview_ComputesMetricsAndAggregatesAttributedRoas()
    {
        var engine = new AudioDaiMeshEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalImpressionsServed > 0);
        Assert.True(overview.AudioCompletionRatePct > 90.0);
        Assert.True(overview.TotalAttributedRevenueUsd > 0);
        Assert.True(overview.ReturnOnAdSpendRoas > 1.0);
        Assert.NotEmpty(overview.ActiveSlots);
        Assert.NotEmpty(overview.RecentAttributions);
    }

    [Fact]
    public void InsertAudioSlot_GeneratesAcousticWatermarkAndInsertsSlot()
    {
        var engine = new AudioDaiMeshEngine();
        var request = new InsertAudioAdRequest(
            "FinTech Frontier Pod",
            "Ep. 42: Next-Gen Payments Infrastructure",
            "MID_ROLL",
            30,
            48.00m,
            "UK-LON"
        );

        var slot = engine.InsertAudioSlot(request);

        Assert.NotNull(slot);
        Assert.StartsWith("SLOT-", slot.SlotId);
        Assert.StartsWith("AWT-", slot.AcousticWatermarkHash);
        Assert.Equal("INSERTED", slot.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveSlots, s => s.SlotId == slot.SlotId);
    }

    [Fact]
    public void RecordAttribution_TracksCrossDeviceConversionAndUpdatesRoas()
    {
        var engine = new AudioDaiMeshEngine();
        var request = new RecordAudioAttributionRequest(
            "SLOT-POD-001",
            "FINTECH2026",
            "SMART_SPEAKER",
            "DEMO_BOOKED",
            24000.00m
        );

        var attribution = engine.RecordAttribution(request);

        Assert.NotNull(attribution);
        Assert.StartsWith("ATTR-", attribution.AttributionId);
        Assert.Equal(24000.00m, attribution.AttributedRevenueUsd);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentAttributions, a => a.AttributionId == attribution.AttributionId);
    }
}
