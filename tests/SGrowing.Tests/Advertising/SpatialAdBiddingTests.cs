using FluentAssertions;
using SGrowing.Api.Modules.Advertising.SpatialAdBidding;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class SpatialAdBiddingTests
{
    private readonly SpatialAdBiddingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsPlacementZonesAndBids()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveSpatialPlacementsCount.Should().Be(2);
        overview.DailyBidsEvaluatedCount.Should().BeGreaterThan(100000);
        overview.AverageSpatialLatencyMs.Should().BeLessThan(30.0);
        overview.PlacementZones.Should().HaveCount(2);
        overview.RecentWinningBids.Should().HaveCount(2);
    }

    [Fact]
    public void EvaluateBid_ClearViewFrustumYieldsPositiveBid()
    {
        var request = new SpatialBidEvaluationRequest(
            "ZONE-UE5-CYBERPUNK-01",
            "Unreal Engine 5",
            12.0, 5.0, -18.0,
            30.0, // Low frustum deviation
            450.0,
            0.05 // Minimal occlusion (5%)
        );

        var result = _engine.EvaluateBid(request);

        result.Should().NotBeNull();
        result.ZoneId.Should().Be("ZONE-UE5-CYBERPUNK-01");
        result.ShouldBid.Should().BeTrue();
        result.OfferedBidCpmUsd.Should().BeGreaterThan(12.50m);
        result.SelectedTextureMapUrl.Should().Contain(".ktx2");
        result.EvaluationLatencyMs.Should().BeLessThan(30);
    }

    [Fact]
    public void VerifySpatialView_LongerDwellPassesVerification()
    {
        var request = new VerifySpatialViewRequest(
            "BID-SPAT-881",
            "ZONE-UE5-CYBERPUNK-01",
            3.5, // 3.5s continuous dwell
            15.0 // 15 deg viewing angle
        );

        var attestation = _engine.VerifySpatialView(request);

        attestation.Should().NotBeNull();
        attestation.AttestationId.Should().StartWith("ATT-GAZE-");
        attestation.ViewabilityVerified.Should().BeTrue();
        attestation.AttestedGazeSeconds.Should().Be(3.5);
        attestation.CryptographicGazeProof.Should().StartWith("0x");
    }
}
