using SGrowing.Api.Modules.Advertising.DoohBidding;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class DoohBiddingTests
{
    [Fact]
    public void GetOverview_AggregatesAudienceMultipliersAndFootTraffic()
    {
        var engine = new DoohBiddingMeshEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveScreensCount >= 2);
        Assert.True(overview.AverageAudienceMultiplier >= 1.5);
        Assert.True(overview.TotalFootTrafficVisitsAttributed > 1000);
        Assert.True(overview.TotalDoohSpendUsd > 0);
        Assert.NotEmpty(overview.ActivePlacements);
        Assert.NotEmpty(overview.RecentAttributions);
    }

    [Fact]
    public void SubmitBid_CalculatesDynamicAudienceMultiplierAndWinsAuction()
    {
        var engine = new DoohBiddingMeshEngine();
        var request = new SubmitDoohBidRequest(
            "Piccadilly Circus Light Spectacular",
            "LON_PICCADILLY",
            "8K_SURROUND",
            12,
            55.00m
        );

        var placement = engine.SubmitBid(request);

        Assert.NotNull(placement);
        Assert.StartsWith("DOOH-LON-", placement.PlacementId);
        Assert.Equal("BID_WON", placement.Status);
        Assert.True(placement.DynamicAudienceMultiplier > 1.5);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActivePlacements, p => p.PlacementId == placement.PlacementId);
    }

    [Fact]
    public void RecordFootTraffic_AttributesGeofencedDeviceGroupVisits()
    {
        var engine = new DoohBiddingMeshEngine();
        var request = new RecordFootTrafficRequest(
            "DOOH-NYC-001",
            "SoHo Concept Store (Broadway)",
            890,
            26.8
        );

        var attribution = engine.RecordFootTraffic(request);

        Assert.NotNull(attribution);
        Assert.StartsWith("ATTR-FT-", attribution.AttributionId);
        Assert.Equal(890, attribution.AttributedStoreVisits);
        Assert.Equal(26.8, attribution.LiftOverBaselinePct);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentAttributions, a => a.AttributionId == attribution.AttributionId);
    }
}
