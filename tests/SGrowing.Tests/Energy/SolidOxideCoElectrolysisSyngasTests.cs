using System;
using SGrowing.Api.Modules.Energy.SolidOxideCoElectrolysisSyngas;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class SolidOxideCoElectrolysisSyngasTests
{
    private readonly SolidOxideCoElectrolysisSyngasEngine _engine = new();

    [Fact]
    public void AnalyzeCoElectrolysisStack_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeCoElectrolysisStack(null!));
    }

    [Fact]
    public void AnalyzeCoElectrolysisStack_TemperatureTooLow_ThrowsArgumentException()
    {
        var telemetry = new SolidOxideCoElectrolysisTelemetry(
            StackId: "SOEC-COLD-01",
            StackOperatingTemperatureCelsius: 350.0, // Too cold for SOEC!
            InletSteamToCo2MolarRatio: 2.0,
            TotalInletFlowRateNlPerMinute: 20.0,
            OperatingCellVoltageVolts: 1.30,
            CurrentDensityAmperesPerSquareCm: 0.8,
            MeasuredOutletSyngasH2ToCoRatio: 2.0,
            AreaSpecificResistanceOhmCm2: 0.25
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeCoElectrolysisStack(telemetry));
    }

    [Fact]
    public void AnalyzeCoElectrolysisStack_OptimalThermoneutral_ReturnsFischerTropschRegime()
    {
        var telemetry = new SolidOxideCoElectrolysisTelemetry(
            StackId: "SOEC-STACK-800C",
            StackOperatingTemperatureCelsius: 800.0,
            InletSteamToCo2MolarRatio: 2.1,
            TotalInletFlowRateNlPerMinute: 30.0,
            OperatingCellVoltageVolts: 1.29, // Matches V_tn ~ 1.285 V
            CurrentDensityAmperesPerSquareCm: 1.10,
            MeasuredOutletSyngasH2ToCoRatio: 2.05, // Ideal 2.05
            AreaSpecificResistanceOhmCm2: 0.20
        );

        var result = _engine.AnalyzeCoElectrolysisStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SOEC-STACK-800C", result.StackId);
        Assert.Equal("THERMONEUTRAL_BALANCED", result.ThermalOperatingMode);
        Assert.True(result.SyngasRatioDivergenceFromTarget <= 0.05);
        Assert.True(result.IsSyngasProductionOptimal);
        Assert.Equal("OPTIMAL_THERMONEUTRAL_FISCHER_TROPSCH_SYNGAS_REGIME", result.CoElectrolysisRegime);
        Assert.Contains("thermoneutral potential", result.StackThermalGuidance);
    }

    [Fact]
    public void AnalyzeCoElectrolysisStack_HighAsrDelamination_ReturnsDelaminationDegradation()
    {
        var telemetry = new SolidOxideCoElectrolysisTelemetry(
            StackId: "SOEC-DEGRADED-02",
            StackOperatingTemperatureCelsius: 800.0,
            InletSteamToCo2MolarRatio: 2.0,
            TotalInletFlowRateNlPerMinute: 20.0,
            OperatingCellVoltageVolts: 1.50,
            CurrentDensityAmperesPerSquareCm: 0.6,
            MeasuredOutletSyngasH2ToCoRatio: 2.0,
            AreaSpecificResistanceOhmCm2: 0.62 // High ASR >= 0.55!
        );

        var result = _engine.AnalyzeCoElectrolysisStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SEVERE_ELECTRODE_DELAMINATION_ASR_DEGRADATION", result.CoElectrolysisRegime);
        Assert.False(result.IsSyngasProductionOptimal);
        Assert.Contains("delamination", result.StackThermalGuidance);
    }

    [Fact]
    public void AnalyzeCoElectrolysisStack_OffSpecSyngas_ReturnsOffSpecHazard()
    {
        var telemetry = new SolidOxideCoElectrolysisTelemetry(
            StackId: "SOEC-OFF-SPEC-03",
            StackOperatingTemperatureCelsius: 750.0,
            InletSteamToCo2MolarRatio: 0.5, // Too little steam!
            TotalInletFlowRateNlPerMinute: 25.0,
            OperatingCellVoltageVolts: 1.32,
            CurrentDensityAmperesPerSquareCm: 0.9,
            MeasuredOutletSyngasH2ToCoRatio: 0.65, // Far below 1.0!
            AreaSpecificResistanceOhmCm2: 0.24
        );

        var result = _engine.AnalyzeCoElectrolysisStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("OFF_SPEC_SYNGAS_COMPOSITION_HAZARD", result.CoElectrolysisRegime);
        Assert.False(result.IsSyngasProductionOptimal);
        Assert.Contains("violates downstream reactor specs", result.StackThermalGuidance);
    }
}
