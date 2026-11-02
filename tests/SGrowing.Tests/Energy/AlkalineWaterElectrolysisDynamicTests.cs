using System;
using SGrowing.Api.Modules.Energy.AlkalineWaterElectrolysisDynamic;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class AlkalineWaterElectrolysisDynamicTests
{
    private readonly AlkalineWaterElectrolysisDynamicEngine _engine = new();

    [Fact]
    public void AnalyzeElectrolysisStack_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeElectrolysisStack(null!));
    }

    [Fact]
    public void AnalyzeElectrolysisStack_InvalidTemperature_ThrowsArgumentException()
    {
        var telemetry = new AlkalineWaterElectrolysisTelemetry(
            StackId: "AWE-STACK-01",
            StackOperatingTemperatureCelsius: -10.0, // Sub-zero!
            KohElectrolyteConcentrationPercent: 30.0,
            CurrentDensityAmperesPerSquareMeter: 4000.0,
            RenewablePowerRampRatePercentPerSecond: 5.0,
            DiaphragmGasCrossoverH2InO2Percent: 0.6,
            MeasuredCellVoltageVolts: 1.80,
            ReverseCurrentProtectionCurrentAmperes: 10.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeElectrolysisStack(telemetry));
    }

    [Fact]
    public void AnalyzeElectrolysisStack_OptimalRegime_ReturnsOptimalAlkalineElectrolysis()
    {
        var telemetry = new AlkalineWaterElectrolysisTelemetry(
            StackId: "AWE-STACK-SOLAR-01",
            StackOperatingTemperatureCelsius: 70.0,
            KohElectrolyteConcentrationPercent: 30.0,
            CurrentDensityAmperesPerSquareMeter: 4500.0,
            RenewablePowerRampRatePercentPerSecond: 2.5,
            DiaphragmGasCrossoverH2InO2Percent: 0.75,
            MeasuredCellVoltageVolts: 1.85,
            ReverseCurrentProtectionCurrentAmperes: 20.0
        );

        var result = _engine.AnalyzeElectrolysisStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("AWE-STACK-SOLAR-01", result.StackId);
        Assert.True(result.SpecificEnergyConsumptionKwhPerNm3 < 4.5);
        Assert.True(result.ExplosionSafetyMarginPercent > 80.0);
        Assert.True(result.IsElectrolyzerStackHealthy);
        Assert.Equal("OPTIMAL_DYNAMIC_ALKALINE_ELECTROLYSIS_REGIME", result.ElectrolyzerRegime);
    }

    [Fact]
    public void AnalyzeElectrolysisStack_ExplosiveGasCrossover_ReturnsCriticalTripHazard()
    {
        var telemetry = new AlkalineWaterElectrolysisTelemetry(
            StackId: "AWE-STACK-FAULT-02",
            StackOperatingTemperatureCelsius: 65.0,
            KohElectrolyteConcentrationPercent: 28.0,
            CurrentDensityAmperesPerSquareMeter: 1200.0,
            RenewablePowerRampRatePercentPerSecond: -20.0,
            DiaphragmGasCrossoverH2InO2Percent: 2.3, // >= 2.0% (50% LEL)
            MeasuredCellVoltageVolts: 1.75,
            ReverseCurrentProtectionCurrentAmperes: 10.0
        );

        var result = _engine.AnalyzeElectrolysisStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CRITICAL_EXPLOSIVE_GAS_CROSSOVER_TRIP_HAZARD", result.ElectrolyzerRegime);
        Assert.False(result.IsElectrolyzerStackHealthy);
        Assert.Contains("nitrogen purge", result.StackProtectionGuidance);
    }

    [Fact]
    public void AnalyzeElectrolysisStack_LowLoadWithoutProtection_ReturnsReverseCurrentRisk()
    {
        var telemetry = new AlkalineWaterElectrolysisTelemetry(
            StackId: "AWE-STACK-WIND-DROP-03",
            StackOperatingTemperatureCelsius: 60.0,
            KohElectrolyteConcentrationPercent: 30.0,
            CurrentDensityAmperesPerSquareMeter: 350.0, // Low load < 800 A/m^2
            RenewablePowerRampRatePercentPerSecond: -35.0,
            DiaphragmGasCrossoverH2InO2Percent: 1.1,
            MeasuredCellVoltageVolts: 1.45,
            ReverseCurrentProtectionCurrentAmperes: 0.0 // No trickle polarization!
        );

        var result = _engine.AnalyzeElectrolysisStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("REVERSE_CURRENT_CATALYST_DEGRADATION_RISK", result.ElectrolyzerRegime);
        Assert.False(result.IsElectrolyzerStackHealthy);
        Assert.Contains("polarization rectifier", result.StackProtectionGuidance);
    }
}
