using System;
using SGrowing.Api.Modules.Aerospace.MartianAerocaptureGuidance;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class MartianAerocaptureGuidanceTests
{
    private readonly MartianAerocaptureGuidanceEngine _engine = new();

    [Fact]
    public void AnalyzeAerocaptureTrajectory_NominalEntryCorridor_MaintainsEquilibriumGlide()
    {
        var telemetry = new MartianAerocaptureTelemetry(
            SpacecraftId: "MARS-SAMPLE-RETURN-ORBITER-01",
            CurrentAltitudeKm: 52.0,
            InertialVelocityMPerSec: 5250.0,
            FlightPathAngleDegrees: -11.6,
            CurrentBankAngleDegrees: 45.0,
            SpacecraftMassKg: 1500.0,
            AerodynamicReferenceAreaM2: 6.5,
            DragCoefficientCd: 1.65,
            LiftToDragRatio: 0.28,
            TargetApoapsisAltitudeKm: 6000.0,
            AtmosphericDustFactor: 0.15,
            MeasuredDecelerationG: 2.8);

        var result = _engine.AnalyzeAerocaptureTrajectory(telemetry);

        Assert.Equal("AEROCAPTURE_TRAJECTORY_NOMINAL", result.AerocaptureCorridorStatus);
        Assert.Equal("MAINTAIN_EQUILIBRIUM_GLIDE_BANK_MODULATION", result.BankAngleGuidanceCommand);
        Assert.True(result.IsOrbitCaptureGuaranteed);
        Assert.True(result.ProjectedExitVelocityMPerSec < 4950.0);
    }

    [Fact]
    public void AnalyzeAerocaptureTrajectory_HighBallisticCoeffSkipRisk_CommandsLiftDownDive()
    {
        // Heavy spacecraft with small aeroshell: beta = 3000 / (4.0 * 1.2) = 625 kg/m2
        // deltaV ~ 250 m/s -> v_exit > 5300 m/s >> 4950 m/s
        var telemetry = new MartianAerocaptureTelemetry(
            SpacecraftId: "MARS-SAMPLE-RETURN-ORBITER-02",
            CurrentAltitudeKm: 58.0,
            InertialVelocityMPerSec: 5650.0,
            FlightPathAngleDegrees: -10.2,
            CurrentBankAngleDegrees: 15.0,
            SpacecraftMassKg: 3000.0,
            AerodynamicReferenceAreaM2: 4.0,
            DragCoefficientCd: 1.20,
            LiftToDragRatio: 0.25,
            TargetApoapsisAltitudeKm: 6000.0,
            AtmosphericDustFactor: 0.05,
            MeasuredDecelerationG: 1.2);

        var result = _engine.AnalyzeAerocaptureTrajectory(telemetry);

        Assert.Equal("CORRIDOR_UNDERSHOOT_SKIP_RISK", result.AerocaptureCorridorStatus);
        Assert.Equal("ROLL_BANK_ANGLE_TO_LIFT_DOWN_FULL_DIVE", result.BankAngleGuidanceCommand);
        Assert.False(result.IsOrbitCaptureGuaranteed);
        Assert.True(result.ProjectedExitVelocityMPerSec >= 4950.0);
    }

    [Fact]
    public void AnalyzeAerocaptureTrajectory_PeakGForceExceeded_CommandsLiftUpPullOut()
    {
        var telemetry = new MartianAerocaptureTelemetry(
            SpacecraftId: "MARS-SAMPLE-RETURN-ORBITER-03",
            CurrentAltitudeKm: 42.0,
            InertialVelocityMPerSec: 5200.0,
            FlightPathAngleDegrees: -12.4,
            CurrentBankAngleDegrees: 75.0,
            SpacecraftMassKg: 1400.0,
            AerodynamicReferenceAreaM2: 6.5,
            DragCoefficientCd: 1.65,
            LiftToDragRatio: 0.28,
            TargetApoapsisAltitudeKm: 6000.0,
            AtmosphericDustFactor: 0.30,
            MeasuredDecelerationG: 5.1); // Exceeds 4.5 g

        var result = _engine.AnalyzeAerocaptureTrajectory(telemetry);

        Assert.Equal("PEAK_DECELERATION_G_LOAD_WARNING", result.AerocaptureCorridorStatus);
        Assert.Equal("ROLL_BANK_ANGLE_TO_LIFT_UP_PULL_OUT", result.BankAngleGuidanceCommand);
        Assert.True(result.IsOrbitCaptureGuaranteed);
    }

    [Fact]
    public void AnalyzeAerocaptureTrajectory_ExcessiveDepletionOvershoot_CommandsLiftUpPullOut()
    {
        // Deep entry: v = 4800 m/s, beta = 80 kg/m2 (light probe, big shield) -> deltaV ~ 1800 m/s -> v_exit = 3000 m/s -> apoapsis < 1500 km
        var telemetry = new MartianAerocaptureTelemetry(
            SpacecraftId: "MARS-SAMPLE-RETURN-ORBITER-04",
            CurrentAltitudeKm: 38.0,
            InertialVelocityMPerSec: 4800.0,
            FlightPathAngleDegrees: -12.8,
            CurrentBankAngleDegrees: 120.0,
            SpacecraftMassKg: 700.0,
            AerodynamicReferenceAreaM2: 7.0,
            DragCoefficientCd: 1.70,
            LiftToDragRatio: 0.28,
            TargetApoapsisAltitudeKm: 6000.0,
            AtmosphericDustFactor: 0.40,
            MeasuredDecelerationG: 3.5);

        var result = _engine.AnalyzeAerocaptureTrajectory(telemetry);

        Assert.Equal("PEAK_DECELERATION_G_LOAD_WARNING", result.AerocaptureCorridorStatus);
        Assert.True(result.ProjectedApoapsisAltitudeKm < 1500.0);
    }
}
