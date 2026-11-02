using SGrowing.Api.Modules.Energy.PemElectrolyzerGasCrossover;
using Xunit;

namespace SGrowing.Tests.Energy;

public class PemElectrolyzerGasCrossoverTests
{
    private readonly PemElectrolyzerEngine _engine = new();

    [Fact]
    public void AnalyzeElectrolyzer_NominalHighCurrent_ReturnsElectrolysisReady()
    {
        var telemetry = new PemElectrolyzerTelemetry
        {
            StackUnitId = "PEM-STACK-MW-001",
            OperatingCurrentDensityAmpsPerCm2 = 2.2,
            OperatingTemperatureCelsius = 80.0,
            OperatingCathodeHydrogenPressureBar = 30.0,
            OperatingAnodeOxygenPressureBar = 2.0,
            MembraneThicknessMicrons = 125.0,
            HydrogenInOxygenAnodeFractionPercent = 0.85, // Well below 1.80%
            TitaniumBipolarPlateContactResistanceMilliOhmCm2 = 14.0, // <= 25 mOhm.cm2
            CellVoltageDegradationMicrovoltsPerHour = 3.8, // <= 15 uV/hr
            WaterFeedConductivityMicroSiemensPerCm = 0.06
        };

        var result = _engine.AnalyzeElectrolyzer(telemetry);

        Assert.Equal("PEM-STACK-MW-001", result.StackUnitId);
        Assert.True(result.IsHighCurrentElectrolysisReady);
        Assert.True(result.IsGasCrossoverSafe);
        Assert.True(result.IsBipolarPlateContactOptimal);
        Assert.True(result.IsMembranePinholeAbsent);
        Assert.True(result.CalculatedOperatingCellVoltageVolts <= 2.10);
        Assert.True(result.StackHighHeatingValueEfficiencyPercent >= 68.0);
        Assert.True(result.HydrogenCrossoverExplosionSafetyMargin >= 4.0); // 4.0 / 0.85 = 4.7
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeElectrolyzer_HighCrossover_TriggersExplosiveHazard()
    {
        var telemetry = new PemElectrolyzerTelemetry
        {
            StackUnitId = "PEM-HAZARD-CROSSOVER",
            HydrogenInOxygenAnodeFractionPercent = 2.15 // Exceeds 1.80%
        };

        var result = _engine.AnalyzeElectrolyzer(telemetry);

        Assert.False(result.IsGasCrossoverSafe);
        Assert.False(result.IsHighCurrentElectrolysisReady);
        Assert.Contains("HYDROGEN_IN_OXYGEN_CROSSOVER_EXPLOSIVE_HAZARD", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeElectrolyzer_HighContactResistance_TriggersPassivationAlert()
    {
        var telemetry = new PemElectrolyzerTelemetry
        {
            StackUnitId = "PEM-PASSIVATION-ALERT",
            TitaniumBipolarPlateContactResistanceMilliOhmCm2 = 38.5 // Exceeds 25.0
        };

        var result = _engine.AnalyzeElectrolyzer(telemetry);

        Assert.False(result.IsBipolarPlateContactOptimal);
        Assert.Contains("ANODE_TITANIUM_BIPOLAR_PASSIVATION_RESISTANCE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeElectrolyzer_PinholeThinning_TriggersMembranePinholeAlert()
    {
        var telemetry = new PemElectrolyzerTelemetry
        {
            StackUnitId = "PEM-PINHOLE-FAIL",
            HydrogenInOxygenAnodeFractionPercent = 2.85, // Severe crossover
            CellVoltageDegradationMicrovoltsPerHour = 22.0 // Exceeds 15 uV/hr
        };

        var result = _engine.AnalyzeElectrolyzer(telemetry);

        Assert.False(result.IsMembranePinholeAbsent);
        Assert.Contains("MEMBRANE_PINHOLE_DEGRADATION_THINNING", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeElectrolyzer_CellVoltage_IncreasesWithCurrentDensity()
    {
        var lowCurrentTelemetry = new PemElectrolyzerTelemetry
        {
            StackUnitId = "PEM-LOW-J",
            OperatingCurrentDensityAmpsPerCm2 = 1.0
        };

        var highCurrentTelemetry = new PemElectrolyzerTelemetry
        {
            StackUnitId = "PEM-HIGH-J",
            OperatingCurrentDensityAmpsPerCm2 = 2.8
        };

        var lowRes = _engine.AnalyzeElectrolyzer(lowCurrentTelemetry);
        var highRes = _engine.AnalyzeElectrolyzer(highCurrentTelemetry);

        Assert.True(highRes.CalculatedOperatingCellVoltageVolts > lowRes.CalculatedOperatingCellVoltageVolts);
    }
}
