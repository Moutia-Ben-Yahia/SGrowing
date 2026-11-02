using SGrowing.Api.Modules.Aerospace.AtmosphericEntryBlackoutGuidance;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class AtmosphericEntryTests
{
    private readonly AtmosphericEntryEngine _engine = new();

    [Fact]
    public void AnalyzeEntry_NominalLiftingCapsule_ReturnsGoForLanding()
    {
        var telemetry = new AtmosphericEntryTelemetry
        {
            EntryVehicleMissionId = "ORION-ARTEMIS-001",
            VehicleConfiguration = "LiftingBody_Capsule",
            FlightPathAngleDegrees = -5.85,
            EntryVelocityKilometersPerSecond = 11.2,
            LiftToDragRatio = 0.38,
            PeakDecelerationGs = 3.85,
            StagnationHeatFluxWattsPerCm2 = 195.0,
            PeakPlasmaSheathElectronDensityPerCm3 = 4.5e13,
            SbandBlackoutDurationSeconds = 240.0,
            IsKaBandRelayTransceiverActive = true,
            MagneticWindowFieldTesla = 0.45
        };

        var result = _engine.AnalyzeEntry(telemetry);

        Assert.NotNull(result);
        Assert.Equal("ORION-ARTEMIS-001", result.EntryVehicleMissionId);
        Assert.True(result.IsSkipOutOvershootAverted);
        Assert.True(result.IsPeakDecelerationThermalSafe);
        Assert.True(result.IsContinuousTelemetryPreserved);
        Assert.True(result.IsEntryGuidanceGoForLanding);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.PlasmaCutoffFrequencyGigaHertz > 15.0);
        Assert.True(result.ReEntryCorridorSafetyMarginPercent >= 70.0);
    }

    [Fact]
    public void AnalyzeEntry_ShallowFlightPathAngle_TriggersSkipOutHazard()
    {
        var telemetry = new AtmosphericEntryTelemetry
        {
            EntryVehicleMissionId = "ORION-ARTEMIS-002",
            FlightPathAngleDegrees = -4.75
        };

        var result = _engine.AnalyzeEntry(telemetry);

        Assert.False(result.IsSkipOutOvershootAverted);
        Assert.False(result.IsEntryGuidanceGoForLanding);
        Assert.Contains("ATMOSPHERIC_SKIP_OUT_TRAJECTORY_HAZARD", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeEntry_ExcessiveAerothermalLoad_TriggersDecelerationBreach()
    {
        var telemetry = new AtmosphericEntryTelemetry
        {
            EntryVehicleMissionId = "ORION-ARTEMIS-003",
            PeakDecelerationGs = 6.2,
            StagnationHeatFluxWattsPerCm2 = 310.0
        };

        var result = _engine.AnalyzeEntry(telemetry);

        Assert.False(result.IsPeakDecelerationThermalSafe);
        Assert.False(result.IsEntryGuidanceGoForLanding);
        Assert.Contains("PEAK_DECELERATION_AEROTHERMAL_BREACH", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeEntry_DensePlasmaWithoutRelay_TriggersTelemetryBlackout()
    {
        var telemetry = new AtmosphericEntryTelemetry
        {
            EntryVehicleMissionId = "ORION-ARTEMIS-004",
            PeakPlasmaSheathElectronDensityPerCm3 = 2.0e14, // Cutoff ~ 127 GHz
            IsKaBandRelayTransceiverActive = false,
            MagneticWindowFieldTesla = 0.10,
            SbandBlackoutDurationSeconds = 280.0
        };

        var result = _engine.AnalyzeEntry(telemetry);

        Assert.False(result.IsContinuousTelemetryPreserved);
        Assert.False(result.IsEntryGuidanceGoForLanding);
        Assert.Contains("TELEMETRY_BLACKOUT_WINDOW_EXCEEDANCE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeEntry_EdgeCorridorAngle_ReducesCorridorMargin()
    {
        var telemetry = new AtmosphericEntryTelemetry
        {
            EntryVehicleMissionId = "ORION-ARTEMIS-005",
            FlightPathAngleDegrees = -5.25
        };

        var result = _engine.AnalyzeEntry(telemetry);

        Assert.True(result.IsSkipOutOvershootAverted);
        Assert.True(result.ReEntryCorridorSafetyMarginPercent < 20.0);
    }
}
