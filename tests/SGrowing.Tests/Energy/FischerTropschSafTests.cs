using SGrowing.Api.Modules.Energy.MicrochannelFischerTropschSaf;
using Xunit;

namespace SGrowing.Tests.Energy;

public class FischerTropschSafTests
{
    private readonly FischerTropschSafEngine _engine = new();

    [Fact]
    public void AnalyzeReactor_NominalMicrochannelSafRun_ReturnsSafCertified()
    {
        var telemetry = new FischerTropschSafTelemetry
        {
            ReactorUnitId = "FT-MICRO-SAF-001",
            CatalystType = "Cobalt_Alumina_Promoted_Co_Re_Al2O3",
            SyngasH2ToCoRatio = 2.06,
            ReactorOperatingTemperatureCelsius = 215.0,
            OperatingPressureBar = 25.0,
            BedHotspotTemperatureExcessCelsius = 4.5,
            AsfChainGrowthProbabilityAlpha = 0.905,
            CarbonMonoxideConversionPercent = 76.5,
            MethaneSelectivityPercent = 6.8,
            SteamToHydrogenPartialPressureRatio = 0.85,
            CatalystDeactivationRatePercentPerDay = 0.018
        };

        var result = _engine.AnalyzeReactor(telemetry);

        Assert.NotNull(result);
        Assert.Equal("FT-MICRO-SAF-001", result.ReactorUnitId);
        Assert.True(result.IsIsothermalHeatRemovalStable);
        Assert.True(result.IsChainGrowthOptimalForSaf);
        Assert.True(result.IsCatalystOxidationPrevented);
        Assert.True(result.IsFischerTropschSafCertified);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.JetFuelKeroseneYieldPercent > 20.0);
    }

    [Fact]
    public void AnalyzeReactor_SevereHotspot_TriggersThermalRunawayAlert()
    {
        var telemetry = new FischerTropschSafTelemetry
        {
            ReactorUnitId = "FT-MICRO-SAF-002",
            BedHotspotTemperatureExcessCelsius = 16.5,
            MethaneSelectivityPercent = 14.2
        };

        var result = _engine.AnalyzeReactor(telemetry);

        Assert.False(result.IsIsothermalHeatRemovalStable);
        Assert.False(result.IsFischerTropschSafCertified);
        Assert.Contains("MICROCHANNEL_THERMAL_RUNAWAY_HOTSPOT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeReactor_HighSteamPressure_TriggersCobaltOxidationAlert()
    {
        var telemetry = new FischerTropschSafTelemetry
        {
            ReactorUnitId = "FT-MICRO-SAF-003",
            SteamToHydrogenPartialPressureRatio = 1.65
        };

        var result = _engine.AnalyzeReactor(telemetry);

        Assert.False(result.IsCatalystOxidationPrevented);
        Assert.False(result.IsFischerTropschSafCertified);
        Assert.Contains("COBALT_CATALYST_OXIDATION_DEACTIVATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeReactor_LowAlphaDeviation_TriggersAsfDeviationAlert()
    {
        var telemetry = new FischerTropschSafTelemetry
        {
            ReactorUnitId = "FT-MICRO-SAF-004",
            AsfChainGrowthProbabilityAlpha = 0.78 // Heavy light-naphtha shift
        };

        var result = _engine.AnalyzeReactor(telemetry);

        Assert.False(result.IsChainGrowthOptimalForSaf);
        Assert.False(result.IsFischerTropschSafCertified);
        Assert.Contains("ASF_CHAIN_GROWTH_SELECTIVITY_DEVIATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeReactor_LowCoConversion_FailsCertification()
    {
        var telemetry = new FischerTropschSafTelemetry
        {
            ReactorUnitId = "FT-MICRO-SAF-005",
            CarbonMonoxideConversionPercent = 62.0
        };

        var result = _engine.AnalyzeReactor(telemetry);

        Assert.False(result.IsFischerTropschSafCertified);
    }
}
