using System;
using SGrowing.Api.Modules.Finance.SabrVolatilitySurfaceCalibration;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class SabrVolatilitySurfaceCalibrationTests
{
    private readonly SabrVolatilitySurfaceCalibrationEngine _engine = new();

    [Fact]
    public void AnalyzeSabrSurface_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeSabrSurface(null!));
    }

    [Fact]
    public void AnalyzeSabrSurface_InvalidRho_ThrowsArgumentException()
    {
        var telemetry = new SabrVolatilityTelemetry(
            SurfaceId: "SPX-SABR-SURFACE",
            ForwardPrice: 100.0,
            StrikePrice: 100.0,
            TimeToMaturityYears: 1.0,
            AlphaInitialVol: 0.20,
            BetaElasticity: 0.5,
            RhoCorrelation: -1.05, // Outside (-1, 1)!
            NuVolOfVol: 0.40
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeSabrSurface(telemetry));
    }

    [Fact]
    public void AnalyzeSabrSurface_WellCalibratedSurface_ReturnsArbitrageFreeSmileRegime()
    {
        var telemetry = new SabrVolatilityTelemetry(
            SurfaceId: "EURUSD-FX-VOL-CUBE",
            ForwardPrice: 1.10,
            StrikePrice: 1.10, // ATM
            TimeToMaturityYears: 1.0,
            AlphaInitialVol: 0.08,
            BetaElasticity: 1.0,
            RhoCorrelation: -0.25,
            NuVolOfVol: 0.35
        );

        var result = _engine.AnalyzeSabrSurface(telemetry);

        Assert.NotNull(result);
        Assert.Equal("EURUSD-FX-VOL-CUBE", result.SurfaceId);
        Assert.True(result.ImpliedBlackVolatilityPercent > 0.0);
        Assert.True(result.IsArbitrageFree);
        Assert.Equal("ARBITRAGE_FREE_CALIBRATED_SABR_SMILE_REGIME", result.VolatilitySurfaceRegime);
        Assert.Contains("arbitrage-free smile", result.CalibrationGuidance);
    }

    [Fact]
    public void AnalyzeSabrSurface_ExtremeNegativeRho_ReturnsDensityLeakageHazard()
    {
        var telemetry = new SabrVolatilityTelemetry(
            SurfaceId: "LEAKY-WING-SURFACE",
            ForwardPrice: 100.0,
            StrikePrice: 60.0, // Far OTM Put
            TimeToMaturityYears: 2.0,
            AlphaInitialVol: 0.25,
            BetaElasticity: 0.5,
            RhoCorrelation: -0.98, // Extreme rho <= -0.95!
            NuVolOfVol: 0.85
        );

        var result = _engine.AnalyzeSabrSurface(telemetry);

        Assert.NotNull(result);
        Assert.Equal("EXTREME_CORRELATION_DENSITY_LEAKAGE_HAZARD", result.VolatilitySurfaceRegime);
        Assert.False(result.IsArbitrageFree);
        Assert.Contains("negative risk-neutral probability density", result.CalibrationGuidance);
    }

    [Fact]
    public void AnalyzeSabrSurface_HighVolOfVolLongTenor_ReturnsWingInstabilityWarning()
    {
        var telemetry = new SabrVolatilityTelemetry(
            SurfaceId: "LONG-TENOR-HIGH-NU",
            ForwardPrice: 50.0,
            StrikePrice: 50.0,
            TimeToMaturityYears: 5.0, // > 3.0 yrs
            AlphaInitialVol: 0.15,
            BetaElasticity: 0.5,
            RhoCorrelation: -0.30,
            NuVolOfVol: 2.1 // > 1.8
        );

        var result = _engine.AnalyzeSabrSurface(telemetry);

        Assert.NotNull(result);
        Assert.Equal("HIGH_VOL_OF_VOL_LONG_TENOR_SMILE_WING_INSTABILITY", result.VolatilitySurfaceRegime);
        Assert.False(result.IsArbitrageFree);
        Assert.Contains("numerical PDE", result.CalibrationGuidance);
    }
}
