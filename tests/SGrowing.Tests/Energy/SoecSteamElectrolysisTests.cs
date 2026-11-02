using SGrowing.Api.Modules.Energy.SoecSteamElectrolysisThermalNeutral;
using Xunit;

namespace SGrowing.Tests.Energy;

public class SoecSteamElectrolysisTests
{
    private readonly SoecSteamElectrolysisEngine _engine = new();

    [Fact]
    public void AnalyzeStack_NominalSoecSteamElectrolyzer_ReturnsCertified()
    {
        var telemetry = new SoecSteamElectrolysisTelemetry
        {
            ElectrolyzerStackUid = "SOEC-STACK-800C-01",
            OperatingTemperatureCelsius = 800.0,
            OperatingCellVoltageVolts = 1.285,
            ThermalNeutralVoltageVolts = 1.290,
            ReversibleNernstPotentialVolts = 1.025,
            CurrentDensityAmperesPerCm2 = 1.15,
            SteamUtilizationPercent = 74.5,
            AreaSpecificResistanceOhmCm2 = 0.22,
            OxygenPartialPressureAtElectrodeBar = 45.0,
            FaradayEfficiencyPercent = 99.1
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SOEC-STACK-800C-01", result.ElectrolyzerStackUid);
        Assert.True(result.IsOperatingInThermalNeutralBand);
        Assert.True(result.IsSteamStarvationAverted);
        Assert.True(result.IsElectrodeDelaminationAverted);
        Assert.True(result.IsSoecElectrolyzerOperationCertified);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.HydrogenProductionRateNm3PerHourPerM2 > 4.5);
    }

    [Fact]
    public void AnalyzeStack_HighOperatingVoltage_TriggersThermalRunawayAlert()
    {
        var telemetry = new SoecSteamElectrolysisTelemetry
        {
            ElectrolyzerStackUid = "SOEC-STACK-800C-02",
            OperatingCellVoltageVolts = 1.425
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsOperatingInThermalNeutralBand);
        Assert.False(result.IsSoecElectrolyzerOperationCertified);
        Assert.Contains("SOEC_EXOTHERMIC_THERMAL_RUNAWAY", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeStack_ExcessiveSteamUtilization_TriggersNickelOxidationAlert()
    {
        var telemetry = new SoecSteamElectrolysisTelemetry
        {
            ElectrolyzerStackUid = "SOEC-STACK-800C-03",
            SteamUtilizationPercent = 89.5
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsSteamStarvationAverted);
        Assert.False(result.IsSoecElectrolyzerOperationCertified);
        Assert.Contains("NICKEL_REOXIDATION_STEAM_STARVATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeStack_HighOxygenOverpressure_TriggersDelaminationAlert()
    {
        var telemetry = new SoecSteamElectrolysisTelemetry
        {
            ElectrolyzerStackUid = "SOEC-STACK-800C-04",
            OxygenPartialPressureAtElectrodeBar = 165.0
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsElectrodeDelaminationAverted);
        Assert.False(result.IsSoecElectrolyzerOperationCertified);
        Assert.Contains("OXYGEN_ELECTRODE_DELAMINATION_OVERPOTENTIAL", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeStack_SubThresholdCurrentDensity_FailsCertification()
    {
        var telemetry = new SoecSteamElectrolysisTelemetry
        {
            ElectrolyzerStackUid = "SOEC-STACK-800C-05",
            CurrentDensityAmperesPerCm2 = 0.65 // < 0.80 A/cm2
        };

        var result = _engine.AnalyzeStack(telemetry);

        Assert.False(result.IsSoecElectrolyzerOperationCertified);
    }
}
