namespace SGrowing.Tests.Aerospace;

using SGrowing.Api.Modules.Aerospace.OislLaserPointingJitter;
using Xunit;

public class OislLaserPointingJitterTests
{
    private readonly OislLaserPointingJitterEngine _engine = new();

    [Fact]
    public void AnalyzeLinkJitter_NominalJitter_MaintainsLockedTrackingState()
    {
        // lambda = 1550 nm, D = 100 mm -> divergence = 18.91 urad
        // jitter = 2.0 urad -> r = 2.0 / 18.91 = 0.106 -> Attenuation ~ 0.10 dB
        var telemetry = new OislLaserTelemetry(
            LinkId: "STARLINK-ISL-ALPHA-01",
            InterSatelliteDistanceKm: 2000.0,
            LaserWavelengthNm: 1550.0,
            TelescopeApertureDiameterMm: 100.0,
            TransmitOpticalPowerMilliWatts: 1000.0,
            PointingJitterRmsMicroRadians: 2.0,
            MeasuredReceivedPowerDbm: -25.0,
            QpdTrackingErrorMicroRadians: 1.5);

        var result = _engine.AnalyzeLinkJitter(telemetry);

        Assert.Equal("STARLINK-ISL-ALPHA-01", result.LinkId);
        Assert.Equal("LOCKED_NOMINAL_TRACKING", result.TrackingState);
        Assert.Equal("ZERO_OFFSET_HOLD", result.FsmCorrectionCommand);
        Assert.False(result.IsLinkDegradationRiskDetected);
        Assert.True(result.PointingJitterRatio < 0.2);
        Assert.True(result.JitterAttenuationDb < 0.5);
    }

    [Fact]
    public void AnalyzeLinkJitter_ElevatedJitter_AppliesPiezoDamping()
    {
        // jitter = 8.5 urad -> r = 8.5 / 18.91 = 0.450 (> 0.35) -> Attenuation ~ 1.76 dB
        var telemetry = new OislLaserTelemetry(
            LinkId: "KUIPER-ISL-BETA-02",
            InterSatelliteDistanceKm: 3000.0,
            LaserWavelengthNm: 1550.0,
            TelescopeApertureDiameterMm: 100.0,
            TransmitOpticalPowerMilliWatts: 1500.0,
            PointingJitterRmsMicroRadians: 8.5,
            MeasuredReceivedPowerDbm: -32.0,
            QpdTrackingErrorMicroRadians: 6.0); // > 5.0 urad

        var result = _engine.AnalyzeLinkJitter(telemetry);

        Assert.True(result.IsLinkDegradationRiskDetected);
        Assert.Equal("JITTER_WARNING_DAMPING_REQUIRED", result.TrackingState);
        Assert.Equal("APPLY_PIEZO_HIGH_BANDWIDTH_DAMPING", result.FsmCorrectionCommand);
        Assert.Contains("High-bandwidth piezo FSM loop activated", result.LinkHealthSummary);
    }

    [Fact]
    public void AnalyzeLinkJitter_SevereJitterBurst_TriggersCoarseGimbalReacquisition()
    {
        // jitter = 16.0 urad -> r = 16.0 / 18.91 = 0.846 (> 0.70) -> Attenuation ~ 6.2 dB
        var telemetry = new OislLaserTelemetry(
            LinkId: "IRIDIUM-ISL-GAMMA-03",
            InterSatelliteDistanceKm: 4000.0,
            LaserWavelengthNm: 1550.0,
            TelescopeApertureDiameterMm: 100.0,
            TransmitOpticalPowerMilliWatts: 2000.0,
            PointingJitterRmsMicroRadians: 16.0,
            MeasuredReceivedPowerDbm: -45.0,
            QpdTrackingErrorMicroRadians: 14.5); // > 12.0 urad

        var result = _engine.AnalyzeLinkJitter(telemetry);

        Assert.True(result.IsLinkDegradationRiskDetected);
        Assert.Equal("CRITICAL_BURST_FSM_SATURATION_ALERT", result.TrackingState);
        Assert.Equal("ENGAGE_COARSE_GIMBAL_RESET_AND_REACQUIRE", result.FsmCorrectionCommand);
        Assert.Contains("CRITICAL PAT ALERT", result.LinkHealthSummary);
    }

    [Fact]
    public void AnalyzeLinkJitter_ZeroDistance_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new OislLaserTelemetry(
            LinkId: "ERR-ZERO-DIST",
            InterSatelliteDistanceKm: 0.0,
            LaserWavelengthNm: 1550.0,
            TelescopeApertureDiameterMm: 100.0,
            TransmitOpticalPowerMilliWatts: 1000.0,
            PointingJitterRmsMicroRadians: 1.0,
            MeasuredReceivedPowerDbm: -20.0,
            QpdTrackingErrorMicroRadians: 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeLinkJitter(invalid));
    }
}
