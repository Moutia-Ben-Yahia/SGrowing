using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.SpaceElevatorTether;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public class SpaceElevatorTetherTests
{
    private readonly SpaceElevatorTetherEngine _engine = new();

    [Fact]
    public void ComputeStabilization_NominalQuiescentTether_MaintainsNominalCadence()
    {
        var state = new TetherDynamicState(
            TetherId: "TETHER-ALPHA-01",
            TetherLengthKm: 100000.0,
            BaseAnchorTensionGigaPascals: 60.0,
            GeoTensionGigaPascals: 72.0,
            MaxAllowableTensionGigaPascals: 100.0,
            TransverseOscillationAmplitudeMeters: 45.0,
            LongitudinalStrainPercent: 0.60,
            ActiveClimbers: []
        );

        var thrusters = new CounterweightThrusterConfig();
        var request = new SpaceElevatorTetherRequest(state, thrusters);
        var plan = _engine.ComputeStabilization(request);

        plan.TetherId.Should().Be("TETHER-ALPHA-01");
        plan.ResonantInstabilityDetected.Should().BeFalse();
        plan.TensionSafetyMarginStatus.Should().Be("NOMINAL_DYNAMIC_STABILITY");
        plan.RecommendedClimberCadenceDirective.Should().Contain("NOMINAL_CADENCE");
        plan.TransverseWaveVelocityKmPerSec.Should().BeGreaterThan(7.0);
        plan.TotalCoriolisDeflectionForceKiloNewtons.Should().Be(0.0);
    }

    [Fact]
    public void ComputeStabilization_HeavyClimberFleet_CalculatesCoriolisAndFiresDamping()
    {
        // 3 heavy climbers ascending at high velocity (0.25 km/s = 900 km/h)
        var climbers = new List<ClimberTelemetry>
        {
            new("CLIMBER-CARGO-1", AltitudeKm: 12000.0, AscentVelocityKmPerSec: 0.25, MassMetricTons: 150.0),
            new("CLIMBER-CARGO-2", AltitudeKm: 28000.0, AscentVelocityKmPerSec: 0.25, MassMetricTons: 150.0),
            new("CLIMBER-CREW-1",  AltitudeKm: 34000.0, AscentVelocityKmPerSec: 0.30, MassMetricTons: 80.0)
        };

        var state = new TetherDynamicState(
            TetherId: "TETHER-HIGH-THROUGHPUT",
            TetherLengthKm: 100000.0,
            BaseAnchorTensionGigaPascals: 65.0,
            GeoTensionGigaPascals: 82.0,
            MaxAllowableTensionGigaPascals: 100.0,
            TransverseOscillationAmplitudeMeters: 550.0, // High oscillation!
            LongitudinalStrainPercent: 0.95,
            ActiveClimbers: climbers
        );

        var thrusters = new CounterweightThrusterConfig(MaxThrustKiloNewtons: 50.0);
        var request = new SpaceElevatorTetherRequest(state, thrusters);
        var plan = _engine.ComputeStabilization(request);

        plan.TotalCoriolisDeflectionForceKiloNewtons.Should().BeGreaterThan(0.0);
        plan.ResonantInstabilityDetected.Should().BeTrue();
        plan.RecommendedClimberCadenceDirective.Should().Contain("THROTTLE_CLIMBER_SPEED");
        plan.ThrusterCommands.Should().Contain(c => c.Axis == "TRANSVERSE_EAST_WEST");
    }

    [Fact]
    public void ComputeStabilization_NearUltimateTensileStrength_AlertsCriticalLimit()
    {
        var climbers = new List<ClimberTelemetry>
        {
            new("CLIMBER-SUPER-HEAVY", AltitudeKm: 35786.0, AscentVelocityKmPerSec: 0.1, MassMetricTons: 600.0)
        };

        var state = new TetherDynamicState(
            TetherId: "TETHER-TENSION-STRESS",
            TetherLengthKm: 100000.0,
            BaseAnchorTensionGigaPascals: 85.0,
            GeoTensionGigaPascals: 91.5, // 91.5 GPa + climber loads > 92 GPa threshold
            MaxAllowableTensionGigaPascals: 100.0,
            TransverseOscillationAmplitudeMeters: 100.0,
            LongitudinalStrainPercent: 1.10,
            ActiveClimbers: climbers
        );

        var thrusters = new CounterweightThrusterConfig();
        var request = new SpaceElevatorTetherRequest(state, thrusters);
        var plan = _engine.ComputeStabilization(request);

        plan.TensionSafetyMarginStatus.Should().Be("CRITICAL_TENSION_LIMIT");
        plan.PeakTensionGigaPascals.Should().BeGreaterThan(92.0);
    }

    [Fact]
    public void ComputeStabilization_NullClimbers_HandlesGracefully()
    {
        var state = new TetherDynamicState("TETHER-EMPTY-FLEET", ActiveClimbers: null);
        var request = new SpaceElevatorTetherRequest(state, new CounterweightThrusterConfig());
        var plan = _engine.ComputeStabilization(request);

        plan.TetherId.Should().Be("TETHER-EMPTY-FLEET");
        plan.TotalCoriolisDeflectionForceKiloNewtons.Should().Be(0.0);
    }
}
