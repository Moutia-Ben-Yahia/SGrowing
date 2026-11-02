namespace SGrowing.Tests.Energy;

using SGrowing.Api.Modules.Energy.SoecCoElectrolysis;
using Xunit;

public class SoecCoElectrolysisTests
{
    private readonly SoecCoElectrolysisEngine _engine = new();

    [Fact]
    public void AnalyzeStack_NominalSyngasProduction_ReportsNominalAndLowCarbonActivity()
    {
        // 800 °C, steam = 30 Nl/min, CO2 = 10 Nl/min -> S/C = 3.0
        // CO = 0.15, CO2 = 0.40 -> low pCO^2 / pCO2
        var telemetry = new SoecCoElectrolysisTelemetry(
            StackId: "SOEC-STACK-001",
            OperatingTemperatureCelsius: 800.0,
            InletSteamFlowRateNlMin: 30.0,
            InletCo2FlowRateNlMin: 10.0,
            CurrentDensityAmpsPerCm2: 1.0,
            CellVoltageVolts: 1.285, // Near 1.287 V thermoneutral
            CoConcentrationMolarFraction: 0.15,
            Co2ConcentrationMolarFraction: 0.40);

        var result = _engine.AnalyzeStack(telemetry);

        Assert.Equal("SOEC-STACK-001", result.StackId);
        Assert.Equal(3.0, result.SteamToCarbonRatio);
        Assert.Equal("THERMONEUTRAL_EQUILIBRIUM", result.ThermalOperatingRegime);
        Assert.False(result.IsCarbonCokingRiskDetected);
        Assert.Equal("NOMINAL_SYNGAS_PRODUCTION", result.OperatingState);
        Assert.Equal("MAINTAIN_NOMINAL_INJECTION", result.RecommendedControlAction);
    }

    [Fact]
    public void AnalyzeStack_SteamStarvation_TriggersCokingWarning()
    {
        // steam = 16 Nl/min, CO2 = 10 Nl/min -> S/C = 1.6 (< 1.8)
        var telemetry = new SoecCoElectrolysisTelemetry(
            StackId: "SOEC-STACK-STARVED",
            OperatingTemperatureCelsius: 800.0,
            InletSteamFlowRateNlMin: 16.0,
            InletCo2FlowRateNlMin: 10.0,
            CurrentDensityAmpsPerCm2: 1.2,
            CellVoltageVolts: 1.20, // Endothermic < 1.287
            CoConcentrationMolarFraction: 0.20,
            Co2ConcentrationMolarFraction: 0.30);

        var result = _engine.AnalyzeStack(telemetry);

        Assert.Equal(1.6, result.SteamToCarbonRatio);
        Assert.Equal("ENDOTHERMIC_HEAT_ABSORBING", result.ThermalOperatingRegime);
        Assert.True(result.IsCarbonCokingRiskDetected);
        Assert.Equal("COKING_WARNING_STEAM_STARVATION", result.OperatingState);
        Assert.Equal("BOOST_STEAM_SWEEP_GAS_FLOW", result.RecommendedControlAction);
    }

    [Fact]
    public void AnalyzeStack_SevereCarbonActivity_TriggersEmergencyCathodeModulation()
    {
        // CO = 0.55, CO2 = 0.10, S/C = 1.2
        var telemetry = new SoecCoElectrolysisTelemetry(
            StackId: "SOEC-CRITICAL-COKING",
            OperatingTemperatureCelsius: 750.0,
            InletSteamFlowRateNlMin: 12.0,
            InletCo2FlowRateNlMin: 10.0,
            CurrentDensityAmpsPerCm2: 1.5,
            CellVoltageVolts: 1.35, // Exothermic Joule heating
            CoConcentrationMolarFraction: 0.55,
            Co2ConcentrationMolarFraction: 0.10);

        var result = _engine.AnalyzeStack(telemetry);

        Assert.True(result.IsCarbonCokingRiskDetected);
        Assert.Equal("EXOTHERMIC_JOULE_HEATING", result.ThermalOperatingRegime);
        Assert.Equal("CRITICAL_COKING_DELAMINATION_ALERT", result.OperatingState);
        Assert.Equal("EMERGENCY_CATHODE_POTENTIAL_MODULATION", result.RecommendedControlAction);
        Assert.Contains("CRITICAL ALERT", result.DiagnosticSummary);
    }

    [Fact]
    public void AnalyzeStack_InvalidTemperature_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new SoecCoElectrolysisTelemetry(
            StackId: "ERR-TEMP",
            OperatingTemperatureCelsius: 300.0, // Below 500 °C
            InletSteamFlowRateNlMin: 20.0,
            InletCo2FlowRateNlMin: 10.0,
            CurrentDensityAmpsPerCm2: 1.0,
            CellVoltageVolts: 1.28,
            CoConcentrationMolarFraction: 0.2,
            Co2ConcentrationMolarFraction: 0.3);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeStack(invalid));
    }
}
