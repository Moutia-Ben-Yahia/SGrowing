using System;
using SGrowing.Api.Modules.Telecom.DirectToCellDopplerTimingAdvance;
using Xunit;

namespace SGrowing.Tests.Telecom;

public sealed class DirectToCellDopplerTimingAdvanceTests
{
    private readonly DirectToCellDopplerTimingAdvanceEngine _engine = new();

    [Fact]
    public void AnalyzeLinkSynchronization_HighElevationNominalLink_ReturnsNominalTracking()
    {
        // fc = 2.0 GHz, v = 7500 m/s, theta = 60 deg -> cos(60) = 0.5
        // fd = 2e9 * (7500 * 0.5 / 299792458) = 25017.3 Hz
        var telemetry = new DirectToCellTelemetry(
            SatelliteId: "STARLINK-D2C-SAT-104",
            UserTerminalId: "UE-5G-PIXEL-78",
            CarrierFrequencyHz: 2.0e9,
            SatelliteOrbitalVelocityMPerSec: 7500.0,
            ElevationAngleDegrees: 60.0,
            SlantRangeKm: 650.0,
            PreCompensatedFrequencyShiftHz: 25000.0,
            TimingDriftRateNsPerSec: 12.0,
            MeasuredPrachResidualDopplerHz: 17.3);

        var result = _engine.AnalyzeLinkSynchronization(telemetry);

        Assert.Equal("NOMINAL_NTN_LINK", result.NtnLinkQualityStatus);
        Assert.Equal("MAINTAIN_EPHEMERIS_GUIDED_BEAM_TRACKING", result.RecommendedSynchronizationAction);
        Assert.False(result.IsHandoverImminent);
        Assert.True(result.ResidualDopplerErrorHz < 100.0);
        Assert.True(result.TimingAdvanceMicroseconds > 4000.0);
    }

    [Fact]
    public void AnalyzeLinkSynchronization_UncompensatedDopplerDiscrepancy_TriggersPreCompensation()
    {
        // theta = 30 deg -> cos(30) = 0.866
        // fd = 2e9 * (7500 * 0.866 / 299792458) = 43330 Hz
        // PreCompensated = 0.0 -> Residual > 40,000 Hz >> 1200 Hz
        var telemetry = new DirectToCellTelemetry(
            SatelliteId: "STARLINK-D2C-SAT-105",
            UserTerminalId: "UE-5G-SAMSUNG-92",
            CarrierFrequencyHz: 2.0e9,
            SatelliteOrbitalVelocityMPerSec: 7500.0,
            ElevationAngleDegrees: 30.0,
            SlantRangeKm: 850.0,
            PreCompensatedFrequencyShiftHz: 0.0,
            TimingDriftRateNsPerSec: 15.0,
            MeasuredPrachResidualDopplerHz: 0.0);

        var result = _engine.AnalyzeLinkSynchronization(telemetry);

        Assert.Equal("EXCESSIVE_DOPPLER_MISMATCH", result.NtnLinkQualityStatus);
        Assert.Equal("APPLY_DOWNLINK_PRE_DOPPLER_COMPENSATION_AND_EXTEND_PRACH_FILTER", result.RecommendedSynchronizationAction);
        Assert.True(result.ResidualDopplerErrorHz > 1200.0);
    }

    [Fact]
    public void AnalyzeLinkSynchronization_HighTimingDriftRate_TriggersMacCeUpdate()
    {
        // Pre-compensated exactly, but timing drift is 45 ns/s (> 30)
        var telemetry = new DirectToCellTelemetry(
            SatelliteId: "STARLINK-D2C-SAT-106",
            UserTerminalId: "UE-5G-IPHONE-33",
            CarrierFrequencyHz: 2.0e9,
            SatelliteOrbitalVelocityMPerSec: 7500.0,
            ElevationAngleDegrees: 45.0,
            SlantRangeKm: 500.0,
            PreCompensatedFrequencyShiftHz: 35380.0,
            TimingDriftRateNsPerSec: 45.0,
            MeasuredPrachResidualDopplerHz: 15.0);

        var result = _engine.AnalyzeLinkSynchronization(telemetry);

        Assert.Equal("TIMING_ADVANCE_DRIFT_ALERT", result.NtnLinkQualityStatus);
        Assert.Equal("DISPATCH_CONTINUOUS_MAC_CE_TIMING_ADVANCE_UPDATE", result.RecommendedSynchronizationAction);
    }

    [Fact]
    public void AnalyzeLinkSynchronization_LowElevationHorizonPass_SetsHandoverImminent()
    {
        var telemetry = new DirectToCellTelemetry(
            SatelliteId: "STARLINK-D2C-SAT-107",
            UserTerminalId: "UE-5G-MODEM-11",
            CarrierFrequencyHz: 2.0e9,
            SatelliteOrbitalVelocityMPerSec: 7500.0,
            ElevationAngleDegrees: 8.5,
            SlantRangeKm: 1200.0,
            PreCompensatedFrequencyShiftHz: 49500.0,
            TimingDriftRateNsPerSec: 20.0,
            MeasuredPrachResidualDopplerHz: 25.0);

        var result = _engine.AnalyzeLinkSynchronization(telemetry);

        Assert.True(result.IsHandoverImminent);
        Assert.True(result.PropagationDelayMicroseconds > 3900.0);
    }
}
