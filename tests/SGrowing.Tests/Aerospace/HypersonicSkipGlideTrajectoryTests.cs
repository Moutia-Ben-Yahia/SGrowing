using System;
using SGrowing.Api.Modules.Aerospace.HypersonicSkipGlideTrajectory;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class HypersonicSkipGlideTrajectoryTests
{
    private readonly HypersonicSkipGlideTrajectoryEngine _engine = new();

    [Fact]
    public void AnalyzeTrajectoryEnvelope_NominalSkipGlideCruise_MaintainsLOverD()
    {
        var telemetry = new HypersonicSkipGlideTelemetry(
            VehicleId: "HGV-WAVERIDER-AVANGARD-01",
            AltitudeKilometers: 55.0,
            VelocityMPerSec: 4500.0,
            FlightPathAngleDegrees: 0.5,
            AngleOfAttackDegrees: 10.0,
            AtmosphericDensityKgPerM3: 1.5e-5,
            NoseRadiusMeters: 0.15,
            VehicleMassKg: 1200.0,
            AerodynamicLiftCoefficient: 0.40,
            AerodynamicDragCoefficient: 0.08,
            ReferenceAreaM2: 2.5,
            TpsNoseCapTemperatureCelsius: 1100.0);

        var result = _engine.AnalyzeTrajectoryEnvelope(telemetry);

        Assert.Equal("NOMINAL_SKIP_GLIDE_TRAJECTORY", result.SkipTrajectoryRegime);
        Assert.Equal("MAINTAIN_MAX_LIFT_OVER_DRAG_CRUISE_ATTITUDE", result.FlightGuidanceRecommendation);
        Assert.True(result.IsThermalStructuralEnvelopeSafe);
        Assert.True(result.LiftToDragRatio >= 4.0);
        Assert.True(result.NormalLoadFactorGForce < 7.5);
    }

    [Fact]
    public void AnalyzeTrajectoryEnvelope_ExcessiveThermalSoak_TriggersPullUpApogeeCommand()
    {
        // Low altitude deep atmospheric entry: rho = 2.5e-3, v = 6200 m/s
        var telemetry = new HypersonicSkipGlideTelemetry(
            VehicleId: "HGV-WAVERIDER-AVANGARD-02",
            AltitudeKilometers: 28.0,
            VelocityMPerSec: 6200.0,
            FlightPathAngleDegrees: -1.5,
            AngleOfAttackDegrees: 12.0,
            AtmosphericDensityKgPerM3: 2.5e-3,
            NoseRadiusMeters: 0.04,
            VehicleMassKg: 1200.0,
            AerodynamicLiftCoefficient: 0.40,
            AerodynamicDragCoefficient: 0.10,
            ReferenceAreaM2: 1.6,
            TpsNoseCapTemperatureCelsius: 1650.0);

        var result = _engine.AnalyzeTrajectoryEnvelope(telemetry);

        Assert.Equal("TPS_THERMAL_SOAK_EXCEEDED", result.SkipTrajectoryRegime);
        Assert.Equal("EXECUTE_HIGH_ALTITUDE_PULL_UP_AND_EXTEND_SKIP_APOGEE", result.FlightGuidanceRecommendation);
        Assert.False(result.IsThermalStructuralEnvelopeSafe);
        Assert.True(result.StagnationHeatFluxMegawattsPerM2 > 4.5);
    }

    [Fact]
    public void AnalyzeTrajectoryEnvelope_HighGForcePullUp_TriggersElevonTrimWarning()
    {
        // Pull-up maneuver: v = 2800 m/s, rho = 2.0e-3, Rn = 0.20 m -> Tw ~ 1425 C <= 1900 C
        // q_inf = 7840 Pa, Lift = 7840 * 2.0 * 1.10 = 17,248 N
        // mass = 200 kg -> nz = 17248 / (200 * 9.80665) = 8.79 g > 7.5 g
        var telemetry = new HypersonicSkipGlideTelemetry(
            VehicleId: "HGV-WAVERIDER-AVANGARD-03",
            AltitudeKilometers: 35.0,
            VelocityMPerSec: 2800.0,
            FlightPathAngleDegrees: 2.0,
            AngleOfAttackDegrees: 18.0,
            AtmosphericDensityKgPerM3: 2.0e-3,
            NoseRadiusMeters: 0.20,
            VehicleMassKg: 200.0,
            AerodynamicLiftCoefficient: 1.10,
            AerodynamicDragCoefficient: 0.25,
            ReferenceAreaM2: 2.0,
            TpsNoseCapTemperatureCelsius: 1400.0);

        var result = _engine.AnalyzeTrajectoryEnvelope(telemetry);

        Assert.Equal("PULL_UP_G_FORCE_WARNING", result.SkipTrajectoryRegime);
        Assert.Equal("TRIM_ELEVONS_TO_REDUCE_ANGLE_OF_ATTACK_3_DEGREES", result.FlightGuidanceRecommendation);
        Assert.False(result.IsThermalStructuralEnvelopeSafe);
        Assert.True(result.NormalLoadFactorGForce > 7.5);
    }

    [Fact]
    public void AnalyzeTrajectoryEnvelope_HighMeasuredTpsTemperature_TriggersThermalWarning()
    {
        var telemetry = new HypersonicSkipGlideTelemetry(
            VehicleId: "HGV-WAVERIDER-AVANGARD-04",
            AltitudeKilometers: 42.0,
            VelocityMPerSec: 5000.0,
            FlightPathAngleDegrees: 0.0,
            AngleOfAttackDegrees: 10.0,
            AtmosphericDensityKgPerM3: 2.0e-4,
            NoseRadiusMeters: 0.05,
            VehicleMassKg: 1000.0,
            AerodynamicLiftCoefficient: 0.35,
            AerodynamicDragCoefficient: 0.08,
            ReferenceAreaM2: 1.5,
            TpsNoseCapTemperatureCelsius: 1950.0); // Exceeds 1900.0 C

        var result = _engine.AnalyzeTrajectoryEnvelope(telemetry);

        Assert.Equal("TPS_THERMAL_SOAK_EXCEEDED", result.SkipTrajectoryRegime);
        Assert.False(result.IsThermalStructuralEnvelopeSafe);
        Assert.True(result.EquilibriumSurfaceTemperatureCelsius >= 1950.0);
    }
}
