using SGrowing.Api.Modules.Aerospace.OpticalInterSatelliteCrosslink;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class OislCrosslinkTests
{
    private readonly OislCrosslinkEngine _engine = new();

    [Fact]
    public void AnalyzeCrosslink_NominalLeoCrosslink_ReturnsPatGo()
    {
        var telemetry = new OislCrosslinkTelemetry
        {
            OpticalTerminalPairId = "OISL-LEO-SAT-101",
            OrbitRegime = "LowEarthOrbit_LEO",
            CrosslinkRangeKilometers = 2850.0,
            OpticalLinkMarginDecibels = 5.8,
            FastSteeringMirrorJitterMicroRadians = 0.62,
            BeaconAcquisitionLockTimeSeconds = 8.4,
            RelativeDopplerFrequencyShiftGigaHertz = 6.4,
            DopplerFrequencySlewRateMegaHertzPerSecond = 8.2,
            CoherentBitErrorRate = 1.2e-9,
            DataThroughputGigaBitsPerSecond = 100.0
        };

        var result = _engine.AnalyzeCrosslink(telemetry);

        Assert.NotNull(result);
        Assert.Equal("OISL-LEO-SAT-101", result.OpticalTerminalPairId);
        Assert.True(result.IsLinkMarginAdequate);
        Assert.True(result.IsPointingJitterTolerated);
        Assert.True(result.IsDopplerTrackingLocked);
        Assert.True(result.IsCrosslinkPatGoForTransmission);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.ThreeSigmaPointingJitterMicroRadians < 2.0);
        Assert.True(result.BitErrorRateLogMargin > 8.0);
    }

    [Fact]
    public void AnalyzeCrosslink_ExcessiveFsmJitter_TriggersPointingBurstFadeAlert()
    {
        var telemetry = new OislCrosslinkTelemetry
        {
            OpticalTerminalPairId = "OISL-LEO-SAT-102",
            FastSteeringMirrorJitterMicroRadians = 1.85,
            CoherentBitErrorRate = 2.5e-3
        };

        var result = _engine.AnalyzeCrosslink(telemetry);

        Assert.False(result.IsPointingJitterTolerated);
        Assert.False(result.IsCrosslinkPatGoForTransmission);
        Assert.Contains("OISL_POINTING_JITTER_BURST_FADE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCrosslink_BeaconSearchTimeout_TriggersSpiralTimeoutAlert()
    {
        var telemetry = new OislCrosslinkTelemetry
        {
            OpticalTerminalPairId = "OISL-LEO-SAT-103",
            BeaconAcquisitionLockTimeSeconds = 28.5
        };

        var result = _engine.AnalyzeCrosslink(telemetry);

        Assert.False(result.IsCrosslinkPatGoForTransmission);
        Assert.Contains("BEACON_ACQUISITION_SPIRAL_SEARCH_TIMEOUT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCrosslink_ExtremeDopplerSlew_TriggersCarrierSlipAlert()
    {
        var telemetry = new OislCrosslinkTelemetry
        {
            OpticalTerminalPairId = "OISL-LEO-SAT-104",
            DopplerFrequencySlewRateMegaHertzPerSecond = 22.0
        };

        var result = _engine.AnalyzeCrosslink(telemetry);

        Assert.False(result.IsDopplerTrackingLocked);
        Assert.False(result.IsCrosslinkPatGoForTransmission);
        Assert.Contains("DOPPLER_CARRIER_FREQUENCY_SLEW_SLIP", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCrosslink_LowOpticalMargin_FailsLinkAdequacy()
    {
        var telemetry = new OislCrosslinkTelemetry
        {
            OpticalTerminalPairId = "OISL-LEO-SAT-105",
            OpticalLinkMarginDecibels = 2.4
        };

        var result = _engine.AnalyzeCrosslink(telemetry);

        Assert.False(result.IsLinkMarginAdequate);
        Assert.False(result.IsCrosslinkPatGoForTransmission);
    }
}
