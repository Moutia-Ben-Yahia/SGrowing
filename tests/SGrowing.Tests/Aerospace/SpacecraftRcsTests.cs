using SGrowing.Api.Modules.Aerospace.SpacecraftRcsPlumeImpingement;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class SpacecraftRcsTests
{
    private readonly SpacecraftRcsEngine _engine = new();

    [Fact]
    public void AnalyzeRcsFiring_NominalProximityOps_ReturnsFiringApproved()
    {
        var telemetry = new SpacecraftRcsTelemetry
        {
            SpacecraftVehicleUid = "ORION-RCS-001",
            PropellantCombination = "MMH_NTO_Hypergolic",
            MinimumImpulseBitDurationMilliseconds = 15.0,
            ValveJitterMilliseconds = 0.85,
            PeakPlumeImpingementPressurePascals = 8.5,
            PlumeHeatFluxKiloWattsPerMeter2 = 3.2,
            InducedDisturbanceTorqueNewtonMeters = 11.4,
            RcsValveSeatTemperatureCelsius = 108.0,
            StationKeepingPwmDutyCyclePercent = 6.5,
            AttitudeDeadbandErrorDegrees = 0.045
        };

        var result = _engine.AnalyzeRcsFiring(telemetry);

        Assert.NotNull(result);
        Assert.Equal("ORION-RCS-001", result.SpacecraftVehicleUid);
        Assert.True(result.IsPlumeImpingementTolerated);
        Assert.True(result.IsRcsValveThermalStateStable);
        Assert.True(result.IsDutyCycleFuelOptimal);
        Assert.True(result.IsRcsFiringApprovedForProximityOps);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.PlumeImpingementSafetyMarginPercent > 20.0);
        Assert.True(result.ValveThermalHeadroomCelsius > 25.0);
    }

    [Fact]
    public void AnalyzeRcsFiring_ExcessivePlumePressure_TriggersPlumeDisturbanceAlert()
    {
        var telemetry = new SpacecraftRcsTelemetry
        {
            SpacecraftVehicleUid = "ORION-RCS-002",
            PeakPlumeImpingementPressurePascals = 16.5,
            InducedDisturbanceTorqueNewtonMeters = 24.0
        };

        var result = _engine.AnalyzeRcsFiring(telemetry);

        Assert.False(result.IsPlumeImpingementTolerated);
        Assert.False(result.IsRcsFiringApprovedForProximityOps);
        Assert.Contains("PLUME_IMPINGEMENT_AERO_DISTURBANCE_TORQUE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeRcsFiring_HighValveTemperature_TriggersValveChatterAlert()
    {
        var telemetry = new SpacecraftRcsTelemetry
        {
            SpacecraftVehicleUid = "ORION-RCS-003",
            RcsValveSeatTemperatureCelsius = 146.0
        };

        var result = _engine.AnalyzeRcsFiring(telemetry);

        Assert.False(result.IsRcsValveThermalStateStable);
        Assert.False(result.IsRcsFiringApprovedForProximityOps);
        Assert.Contains("RCS_VALVE_THERMAL_SOAKBACK_CHATTER", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeRcsFiring_ExcessivePwmDuty_TriggersPropellantBlowoutAlert()
    {
        var telemetry = new SpacecraftRcsTelemetry
        {
            SpacecraftVehicleUid = "ORION-RCS-004",
            StationKeepingPwmDutyCyclePercent = 18.5
        };

        var result = _engine.AnalyzeRcsFiring(telemetry);

        Assert.False(result.IsDutyCycleFuelOptimal);
        Assert.False(result.IsRcsFiringApprovedForProximityOps);
        Assert.Contains("LIMIT_CYCLE_PROPELLANT_CONSUMPTION_BLOWOUT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeRcsFiring_SubThresholdImpulseBit_FailsApproval()
    {
        var telemetry = new SpacecraftRcsTelemetry
        {
            SpacecraftVehicleUid = "ORION-RCS-005",
            MinimumImpulseBitDurationMilliseconds = 6.0 // < 10 ms
        };

        var result = _engine.AnalyzeRcsFiring(telemetry);

        Assert.False(result.IsRcsFiringApprovedForProximityOps);
    }
}
