using SGrowing.Api.Modules.Energy.SolidOxideFuelCellTriplePhaseBoundary;
using Xunit;

namespace SGrowing.Tests.Energy;

public class SolidOxideFuelCellTests
{
    private readonly SolidOxideFuelCellEngine _engine = new();

    [Fact]
    public void AnalyzeFuelCell_NominalCermetOperation_ReturnsHighEfficiency()
    {
        var telemetry = new SolidOxideFuelCellTelemetry
        {
            StackId = "SOFC-STACK-001",
            OperatingTemperatureCelsius = 750.0,
            CurrentDensityAmpsPerCm2 = 0.65,
            SteamToCarbonRatio = 2.4, // Safe >= 2.0
            TriplePhaseBoundaryLengthMicronsPerMicron2 = 6.8, // Good >= 4.5
            AreaSpecificResistanceOhmCm2 = 0.22,
            AnodeInletMethaneFractionPercent = 25.0,
            FuelUtilizationPercent = 70.0,
            AnodeOxygenPartialPressureAtm = 1.0e-17, // Protective
            InternalReformingTemperatureGradientCelsius = 15.0
        };

        var result = _engine.AnalyzeFuelCell(telemetry);

        Assert.Equal("SOFC-STACK-001", result.StackId);
        Assert.True(result.IsStackHighEfficiencyOperating);
        Assert.True(result.IsSteamToCarbonRatioSafe);
        Assert.True(result.IsTriplePhaseBoundaryIntact);
        Assert.True(result.IsNickelAnodeProtectedFromOxidation);
        Assert.True(result.CalculatedOperatingCellVoltageVolts >= 0.65);
        Assert.True(result.StackPowerDensityWattsPerCm2 > 0.40);
        Assert.True(result.CokingRiskIndex <= 2.5);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeFuelCell_LowSteamToCarbon_TriggersCokingDeactivation()
    {
        var telemetry = new SolidOxideFuelCellTelemetry
        {
            StackId = "SOFC-COKING-ALERT",
            SteamToCarbonRatio = 1.4 // Dangerous < 2.0
        };

        var result = _engine.AnalyzeFuelCell(telemetry);

        Assert.False(result.IsSteamToCarbonRatioSafe);
        Assert.False(result.IsStackHighEfficiencyOperating);
        Assert.Contains("ANODE_CARBON_COKING_DEACTIVATION", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeFuelCell_DegradedTpb_TriggersSinteringAlert()
    {
        var telemetry = new SolidOxideFuelCellTelemetry
        {
            StackId = "SOFC-SINTERING-DEGRADED",
            TriplePhaseBoundaryLengthMicronsPerMicron2 = 2.1 // Below 4.5
        };

        var result = _engine.AnalyzeFuelCell(telemetry);

        Assert.False(result.IsTriplePhaseBoundaryIntact);
        Assert.Contains("TRIPLE_PHASE_BOUNDARY_SINTERING_DEGRADATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeFuelCell_HighAnodeOxygenPressure_TriggersNickelOxidation()
    {
        var telemetry = new SolidOxideFuelCellTelemetry
        {
            StackId = "SOFC-OXIDATION-RISK",
            AnodeOxygenPartialPressureAtm = 1.0e-12 // Dangerous > 1.0e-14
        };

        var result = _engine.AnalyzeFuelCell(telemetry);

        Assert.False(result.IsNickelAnodeProtectedFromOxidation);
        Assert.Contains("NICKEL_REOXIDATION_VOLUME_FRACTURE_RISK", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeFuelCell_OperatingVoltage_DecreasesWithCurrentDensity()
    {
        var lowCurrentTelemetry = new SolidOxideFuelCellTelemetry
        {
            StackId = "SOFC-LOW-J",
            CurrentDensityAmpsPerCm2 = 0.3
        };

        var highCurrentTelemetry = new SolidOxideFuelCellTelemetry
        {
            StackId = "SOFC-HIGH-J",
            CurrentDensityAmpsPerCm2 = 0.95
        };

        var lowRes = _engine.AnalyzeFuelCell(lowCurrentTelemetry);
        var highRes = _engine.AnalyzeFuelCell(highCurrentTelemetry);

        Assert.True(lowRes.CalculatedOperatingCellVoltageVolts > highRes.CalculatedOperatingCellVoltageVolts);
    }
}
