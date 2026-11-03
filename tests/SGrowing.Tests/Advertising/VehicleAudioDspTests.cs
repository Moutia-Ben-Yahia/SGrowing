using SGrowing.Api.Modules.Advertising.VehicleAudioDsp;
using Xunit;

namespace SGrowing.Tests.Advertising;

public sealed class VehicleAudioDspTests
{
    private readonly VehicleAudioDspEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededConnectedVehiclesAndBids()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ConnectedVehiclesActiveCount >= 1000);
        Assert.True(overview.AverageInCabinCpmUsd > 15.0);
        Assert.NotEmpty(overview.RecentBids);
        Assert.NotEmpty(overview.RecentAttestations);
    }

    [Fact]
    public void BidInCabinAudioSlot_TrafficCongestion_YieldsPremiumContextualCpm()
    {
        var bid = _engine.BidInCabinAudioSlot("BMW iX (iDrive 9)", CommuteContextType.MorningRushCongestion, 12.0, 37.7833, -122.4167);

        Assert.NotNull(bid);
        Assert.StartsWith("BID-VEH-", bid.BidId);
        Assert.Equal(CommuteContextType.MorningRushCongestion, bid.ContextType);
        Assert.True(bid.WinningBidCpmUsd >= 28.0);
        Assert.Contains(".aac", bid.AudioCreativeAssetUri);
    }

    [Fact]
    public void LogPlayoutAttestation_ValidatesHardwareAndVolume()
    {
        var att = _engine.LogPlayoutAttestation("BID-VEH-TEST", "VIN-TESLA-34120", 15, -14.0);

        Assert.NotNull(att);
        Assert.StartsWith("ATT-VEH-", att.AttestationId);
        Assert.True(att.IsPlayoutVerified);
        Assert.True(att.PassengerPresenceDetected);
        Assert.Equal(-14.0, att.AudioVolumeNormalizedDb);
    }
}
