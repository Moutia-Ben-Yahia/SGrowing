using SGrowing.Api.Modules.Finance.SviVolatilitySurfaceCalibration;
using Xunit;

namespace SGrowing.Tests.Finance;

public class SviVolatilityTests
{
    private readonly SviVolatilityEngine _engine = new();

    [Fact]
    public void AnalyzeSurface_NominalSpxOptions_ReturnsCalibratedSurface()
    {
        var telemetry = new SviVolatilityTelemetry
        {
            OptionSurfaceUid = "SVI-SPX-6M-001",
            UnderlyingAsset = "SPX_Index_Options",
            ExpiryTenorYears = 0.50,
            SviParameterA = 0.04,
            SviParameterB = 0.18,
            SviParameterRho = -0.65,
            SviParameterM = 0.02,
            SviParameterSigma = 0.22,
            MinimumButterflyDensityG = 0.025,
            CalibrationRmseBasisPoints = 12.4,
            IsCalendarSpreadMonotonic = true
        };

        var result = _engine.AnalyzeSurface(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SVI-SPX-6M-001", result.OptionSurfaceUid);
        Assert.True(result.IsSviParametersAdmissible);
        Assert.True(result.IsButterflyArbitrageFree);
        Assert.True(result.IsCalendarSpreadArbitrageFree);
        Assert.True(result.IsVolatilitySurfaceCalibrated);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.AsymptoticPutWingSlope > 0.0);
        Assert.True(result.AsymptoticCallWingSlope > 0.0);
    }

    [Fact]
    public void AnalyzeSurface_NegativeButterflyDensity_TriggersButterflyArbitrageAlert()
    {
        var telemetry = new SviVolatilityTelemetry
        {
            OptionSurfaceUid = "SVI-SPX-6M-002",
            MinimumButterflyDensityG = -0.015
        };

        var result = _engine.AnalyzeSurface(telemetry);

        Assert.False(result.IsButterflyArbitrageFree);
        Assert.False(result.IsVolatilitySurfaceCalibrated);
        Assert.Contains("SVI_BUTTERFLY_ARBITRAGE_DENSITY_NEGATIVE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSurface_CalendarVarianceInversion_TriggersCalendarSpreadAlert()
    {
        var telemetry = new SviVolatilityTelemetry
        {
            OptionSurfaceUid = "SVI-SPX-6M-003",
            IsCalendarSpreadMonotonic = false
        };

        var result = _engine.AnalyzeSurface(telemetry);

        Assert.False(result.IsCalendarSpreadArbitrageFree);
        Assert.False(result.IsVolatilitySurfaceCalibrated);
        Assert.Contains("CALENDAR_SPREAD_VOLATILITY_INVERSION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSurface_ExcessiveWingSlope_TriggersRogerLeeBreachAlert()
    {
        var telemetry = new SviVolatilityTelemetry
        {
            OptionSurfaceUid = "SVI-SPX-6M-004",
            SviParameterB = 1.45,
            SviParameterRho = -0.80 // Put slope = 1.45 * 1.80 = 2.61 > 2.0
        };

        var result = _engine.AnalyzeSurface(telemetry);

        Assert.False(result.IsVolatilitySurfaceCalibrated);
        Assert.Contains("SVI_WINGS_ASYMPTOTIC_SLOPE_BREACH", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeSurface_InadmissibleCorrelation_FailsAdmissibility()
    {
        var telemetry = new SviVolatilityTelemetry
        {
            OptionSurfaceUid = "SVI-SPX-6M-005",
            SviParameterRho = 1.15 // |rho| >= 1
        };

        var result = _engine.AnalyzeSurface(telemetry);

        Assert.False(result.IsSviParametersAdmissible);
        Assert.False(result.IsVolatilitySurfaceCalibrated);
    }
}
