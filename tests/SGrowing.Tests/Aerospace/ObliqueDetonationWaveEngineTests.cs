using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.ObliqueDetonationWaveEngine;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class ObliqueDetonationWaveEngineTests
{
    private readonly ObliqueDetonationWaveEngine _engine = new();

    [Fact]
    public void AnalyzeDetonationWaveState_NominalHypersonic_ReturnsStandingDetonation()
    {
        var telemetry = new ObliqueDetonationTelemetry(
            EngineId: "ODWE-MACH9-01",
            FlightMachNumber: 8.5,
            CombustorInletTotalPressureKPa: 180.0,
            CombustorInletTotalTemperatureKelvin: 1450.0,
            RampDeflectionAngleDegrees: 22.0,
            HydrogenFuelEquivalenceRatio: 1.05,
            InductionZoneLengthMillimeters: 25.0,
            CombustorLengthMillimeters: 400.0,
            VariableCowlRampActuatorActive: true
        );

        var result = _engine.AnalyzeDetonationWaveState(telemetry);

        result.CombustionRegime.Should().Be("STANDING_OBLIQUE_DETONATION_OPTIMAL");
        result.IsPropulsionStable.Should().BeTrue();
        result.CombustorStaticPressureRatio.Should().BeGreaterThan(1.0);
        result.HeatReleaseEfficiency.Should().BeGreaterThan(0.90);
    }

    [Fact]
    public void AnalyzeDetonationWaveState_ExcessiveRampAngle_ReturnsDetachedBowShockDanger()
    {
        var telemetry = new ObliqueDetonationTelemetry(
            EngineId: "ODWE-MACH8-DETACH",
            FlightMachNumber: 7.0,
            CombustorInletTotalPressureKPa: 220.0,
            CombustorInletTotalTemperatureKelvin: 1600.0,
            RampDeflectionAngleDegrees: 32.0, // Exceeds shock detachment limit
            HydrogenFuelEquivalenceRatio: 1.10,
            InductionZoneLengthMillimeters: 30.0,
            CombustorLengthMillimeters: 350.0,
            VariableCowlRampActuatorActive: true
        );

        var result = _engine.AnalyzeDetonationWaveState(telemetry);

        result.CombustionRegime.Should().Be("DETACHED_BOW_SHOCK_UNSTART_DANGER");
        result.IsPropulsionStable.Should().BeFalse();
        result.RampActuationCommand.Should().Contain("Retract variable cowl ramp");
    }

    [Fact]
    public void AnalyzeDetonationWaveState_LongInductionLength_ReturnsInductionBlowout()
    {
        var telemetry = new ObliqueDetonationTelemetry(
            EngineId: "ODWE-BLOWOUT-03",
            FlightMachNumber: 9.5,
            CombustorInletTotalPressureKPa: 120.0,
            CombustorInletTotalTemperatureKelvin: 1100.0, // Cold mixture slows chemical kinetics
            RampDeflectionAngleDegrees: 18.0,
            HydrogenFuelEquivalenceRatio: 0.90,
            InductionZoneLengthMillimeters: 280.0, // 280 / 300 = 93% > 65%
            CombustorLengthMillimeters: 300.0,
            VariableCowlRampActuatorActive: true
        );

        var result = _engine.AnalyzeDetonationWaveState(telemetry);

        result.CombustionRegime.Should().Be("INDUCTION_BLOWOUT_UNSTABLE");
        result.IsPropulsionStable.Should().BeFalse();
        result.RampActuationCommand.Should().Contain("Advance upstream plasma igniters");
    }

    [Fact]
    public void AnalyzeDetonationWaveState_UltraLeanFuel_ReturnsDeflagrationCrossover()
    {
        var telemetry = new ObliqueDetonationTelemetry(
            EngineId: "ODWE-LEAN-04",
            FlightMachNumber: 8.0,
            CombustorInletTotalPressureKPa: 150.0,
            CombustorInletTotalTemperatureKelvin: 1350.0,
            RampDeflectionAngleDegrees: 20.0,
            HydrogenFuelEquivalenceRatio: 0.35, // Very lean (<0.45)
            InductionZoneLengthMillimeters: 40.0,
            CombustorLengthMillimeters: 400.0,
            VariableCowlRampActuatorActive: true
        );

        var result = _engine.AnalyzeDetonationWaveState(telemetry);

        result.CombustionRegime.Should().Be("DEFLAGRATION_CROSSOVER_DEGRADED");
        result.IsPropulsionStable.Should().BeFalse();
        result.RampActuationCommand.Should().Contain("Enrichen H2 injection");
    }
}
