using System;
using SGrowing.Api.Modules.Quantum.BosonicCatQubitProtection;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class BosonicCatQubitProtectionTests
{
    private readonly BosonicCatQubitProtectionEngine _engine = new();

    [Fact]
    public void AnalyzeCatQubitProtection_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeCatQubitProtection(null!));
    }

    [Fact]
    public void AnalyzeCatQubitProtection_InvalidPhotonNumber_ThrowsArgumentException()
    {
        var telemetry = new BosonicCatQubitTelemetry(
            CavityQubitId: "KERR-CAT-01",
            CatStateMeanPhotonNumber: -1.0, // Negative!
            SinglePhotonLossRateKappa1Kilohertz: 5.0,
            TwoPhotonDriveRateEpsilon2Kilohertz: 400.0,
            TwoPhotonDissipationRateKappa2Kilohertz: 100.0,
            MeasuredBitFlipLifetimeMilliseconds: 500.0,
            MeasuredPhaseFlipLifetimeMicroseconds: 5.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeCatQubitProtection(telemetry));
    }

    [Fact]
    public void AnalyzeCatQubitProtection_HighBiasCat_ReturnsExponentialBitFlipSuppressedRegime()
    {
        var telemetry = new BosonicCatQubitTelemetry(
            CavityQubitId: "CAT-RESONATOR-ALPHA4",
            CatStateMeanPhotonNumber: 4.8,
            SinglePhotonLossRateKappa1Kilohertz: 3.5,
            TwoPhotonDriveRateEpsilon2Kilohertz: 500.0,
            TwoPhotonDissipationRateKappa2Kilohertz: 140.0,
            MeasuredBitFlipLifetimeMilliseconds: 1200.0, // > 1 sec
            MeasuredPhaseFlipLifetimeMicroseconds: 3.5 // 3.5 us -> bias = 1200 / 0.0035 = ~342,000x
        );

        var result = _engine.AnalyzeCatQubitProtection(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CAT-RESONATOR-ALPHA4", result.CavityQubitId);
        Assert.True(result.NoiseBiasRatio >= 10000.0);
        Assert.True(result.IsNoiseBiasPreserved);
        Assert.Equal("EXPONENTIAL_BIT_FLIP_SUPPRESSED_HIGH_BIAS_CAT_REGIME", result.CatQubitRegime);
        Assert.Contains("Exponential bit-flip protection achieved", result.ErrorCorrectionGuidance);
    }

    [Fact]
    public void AnalyzeCatQubitProtection_SmallPhotonNumber_ReturnsCollapsedVacuumLimit()
    {
        var telemetry = new BosonicCatQubitTelemetry(
            CavityQubitId: "CAT-TINY-02",
            CatStateMeanPhotonNumber: 0.45, // < 1.0!
            SinglePhotonLossRateKappa1Kilohertz: 5.0,
            TwoPhotonDriveRateEpsilon2Kilohertz: 50.0,
            TwoPhotonDissipationRateKappa2Kilohertz: 80.0,
            MeasuredBitFlipLifetimeMilliseconds: 0.1,
            MeasuredPhaseFlipLifetimeMicroseconds: 10.0
        );

        var result = _engine.AnalyzeCatQubitProtection(telemetry);

        Assert.NotNull(result);
        Assert.Equal("COLLAPSED_VACUUM_LIMIT_INSUFFICIENT_CONFINEMENT", result.CatQubitRegime);
        Assert.False(result.IsNoiseBiasPreserved);
        Assert.Contains("distinct coherent state manifolds", result.ErrorCorrectionGuidance);
    }

    [Fact]
    public void AnalyzeCatQubitProtection_HighLossRate_ReturnsLossRateDecoherenceHazard()
    {
        var telemetry = new BosonicCatQubitTelemetry(
            CavityQubitId: "CAT-LOSSY-03",
            CatStateMeanPhotonNumber: 3.0,
            SinglePhotonLossRateKappa1Kilohertz: 75.0, // > 50 kHz!
            TwoPhotonDriveRateEpsilon2Kilohertz: 300.0,
            TwoPhotonDissipationRateKappa2Kilohertz: 100.0,
            MeasuredBitFlipLifetimeMilliseconds: 25.0,
            MeasuredPhaseFlipLifetimeMicroseconds: 0.8
        );

        var result = _engine.AnalyzeCatQubitProtection(telemetry);

        Assert.NotNull(result);
        Assert.Equal("HIGH_SINGLE_PHOTON_LOSS_RATE_DECOHERENCE_HAZARD", result.CatQubitRegime);
        Assert.False(result.IsNoiseBiasPreserved);
        Assert.Contains("Purcell filter coupling", result.ErrorCorrectionGuidance);
    }
}
