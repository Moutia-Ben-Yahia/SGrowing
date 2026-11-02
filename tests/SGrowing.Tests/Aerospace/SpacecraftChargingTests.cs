using SGrowing.Api.Modules.Aerospace.SpacecraftElectrostaticCharging;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class SpacecraftChargingTests
{
    private readonly SpacecraftChargingEngine _engine = new();

    [Fact]
    public void AnalyzeCharging_NominalGeoSubstormPost_ReturnsElectrostaticSafe()
    {
        var telemetry = new SpacecraftChargingTelemetry
        {
            SpacecraftSubsystemId = "GEO-COMM-SAT-01",
            OrbitEnvironment = "GeosynchronousSubstorm_GEO",
            SpacecraftFramePotentialVolts = -85.0,
            DifferentialDielectricPotentialVolts = 280.0,
            EnergeticElectronFluxPerCm2Sec = 4.5e7,
            DeepDielectricCurrentDensityPicoAmpsPerCm2 = 0.95,
            PlasmaContactorEmissionCurrentMilliAmps = 22.5,
            SolarArrayInterconnectElectricFieldVoltsPerCm = 1800.0,
            ArcDischargeTransientRatePerHour = 0.0,
            IsHollowCathodeNeutralizerActive = true
        };

        var result = _engine.AnalyzeCharging(telemetry);

        Assert.NotNull(result);
        Assert.Equal("GEO-COMM-SAT-01", result.SpacecraftSubsystemId);
        Assert.True(result.IsDifferentialPotentialSafe);
        Assert.True(result.IsDeepDielectricChargingSuppressed);
        Assert.True(result.IsPlasmaContactorOperational);
        Assert.True(result.IsSpacecraftElectrostaticSafe);
        Assert.Empty(result.ActiveRegimes);
        Assert.Equal(220.0, result.EsdBreakdownVoltageMarginVolts);
        Assert.True(result.FramePotentialSafetyMarginVolts > 900.0);
    }

    [Fact]
    public void AnalyzeCharging_HighDifferentialVoltage_TriggersEsdArcAlert()
    {
        var telemetry = new SpacecraftChargingTelemetry
        {
            SpacecraftSubsystemId = "GEO-COMM-SAT-02",
            DifferentialDielectricPotentialVolts = 680.0,
            ArcDischargeTransientRatePerHour = 1.4
        };

        var result = _engine.AnalyzeCharging(telemetry);

        Assert.False(result.IsDifferentialPotentialSafe);
        Assert.False(result.IsSpacecraftElectrostaticSafe);
        Assert.Contains("DIFFERENTIAL_ESD_ARC_BREAKDOWN_SURGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCharging_HighDeepCurrentDensity_TriggersDeepChargingAlert()
    {
        var telemetry = new SpacecraftChargingTelemetry
        {
            SpacecraftSubsystemId = "GEO-COMM-SAT-03",
            DeepDielectricCurrentDensityPicoAmpsPerCm2 = 3.8
        };

        var result = _engine.AnalyzeCharging(telemetry);

        Assert.False(result.IsDeepDielectricChargingSuppressed);
        Assert.False(result.IsSpacecraftElectrostaticSafe);
        Assert.Contains("SUBSTORM_GEOSYNCHRONOUS_DEEP_CHARGING", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCharging_DepletedPlasmaContactor_TriggersNeutralizerDepletionAlert()
    {
        var telemetry = new SpacecraftChargingTelemetry
        {
            SpacecraftSubsystemId = "GEO-COMM-SAT-04",
            IsHollowCathodeNeutralizerActive = false,
            PlasmaContactorEmissionCurrentMilliAmps = 2.0,
            SpacecraftFramePotentialVolts = -1250.0
        };

        var result = _engine.AnalyzeCharging(telemetry);

        Assert.False(result.IsPlasmaContactorOperational);
        Assert.False(result.IsSpacecraftElectrostaticSafe);
        Assert.Contains("PLASMA_CONVENTIONAL_NEUTRALIZER_DEPLETION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCharging_NegativeFramePotentialExceedance_FailsSafety()
    {
        var telemetry = new SpacecraftChargingTelemetry
        {
            SpacecraftSubsystemId = "GEO-COMM-SAT-05",
            SpacecraftFramePotentialVolts = -380.0 // Beyond -250V floor
        };

        var result = _engine.AnalyzeCharging(telemetry);

        Assert.False(result.IsSpacecraftElectrostaticSafe);
    }
}
