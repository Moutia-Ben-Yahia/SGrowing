namespace SGrowing.Tests.Quantum;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Quantum.NeutralAtomRydbergGates;
using Xunit;

public class NeutralAtomRydbergGatesTests
{
    private readonly NeutralAtomRydbergGatesEngine _engine = new();

    [Fact]
    public void AnalyzeRydbergGate_NominalParameters_ReturnsOptimalEntanglementRegime()
    {
        var telemetry = new NeutralAtomRydbergTelemetry(
            RegisterId: "RYD-REG-01",
            NeutralAtomQubitCount: 256,
            InteratomicSpacingMicrometers: 3.8,
            RydbergPrincipalQuantumNumberN: 70.0,
            LaserRabiFrequencyMegahertz: 4.5,
            VanDerWaalsC6CoefficientGhzMicrometer6: 850.0,
            SpontaneousEmissionLifetimeMicroseconds: 140.0,
            OpticalTweezerPositionJitterNanometers: 35.0
        );

        var result = _engine.AnalyzeRydbergGate(telemetry);

        result.RegisterId.Should().Be("RYD-REG-01");
        result.RydbergGateRegime.Should().Be("OPTIMAL_RYDBERG_BLOCKADE_ENTANGLEMENT_REGIME");
        result.IsGateFidelityHigh.Should().BeTrue();
        result.RydbergBlockadeRadiusMicrometers.Should().BeGreaterThan(7.0);
        result.CalculatedTwoQubitGateFidelityPercent.Should().BeGreaterThan(99.0);
        result.SpontaneousEmissionInfidelityBasisPoints.Should().BeLessThan(120.0);
        result.ControlGuidance.Should().ContainEquivalentOf("High-fidelity entangling CZ/CNOT");
    }

    [Fact]
    public void AnalyzeRydbergGate_LargeSpacing_ReturnsBlockadeBreakdown()
    {
        var telemetry = new NeutralAtomRydbergTelemetry(
            RegisterId: "RYD-REG-02",
            NeutralAtomQubitCount: 128,
            InteratomicSpacingMicrometers: 9.5, // Exceeds blockade radius ~ 7.5 um
            RydbergPrincipalQuantumNumberN: 70.0,
            LaserRabiFrequencyMegahertz: 4.5,
            VanDerWaalsC6CoefficientGhzMicrometer6: 850.0,
            SpontaneousEmissionLifetimeMicroseconds: 140.0,
            OpticalTweezerPositionJitterNanometers: 35.0
        );

        var result = _engine.AnalyzeRydbergGate(telemetry);

        result.RydbergGateRegime.Should().Be("RYDBERG_BLOCKADE_BREAKDOWN_CROSS_TALK");
        result.IsGateFidelityHigh.Should().BeFalse();
        result.ControlGuidance.Should().ContainEquivalentOf("Reduce tweezer pitch");
    }

    [Fact]
    public void AnalyzeRydbergGate_HighPositionJitter_ReturnsJitterDephasingWarning()
    {
        var telemetry = new NeutralAtomRydbergTelemetry(
            RegisterId: "RYD-REG-03",
            NeutralAtomQubitCount: 64,
            InteratomicSpacingMicrometers: 4.0,
            RydbergPrincipalQuantumNumberN: 70.0,
            LaserRabiFrequencyMegahertz: 4.5,
            VanDerWaalsC6CoefficientGhzMicrometer6: 850.0,
            SpontaneousEmissionLifetimeMicroseconds: 140.0,
            OpticalTweezerPositionJitterNanometers: 165.0 // Exceeds 120 nm
        );

        var result = _engine.AnalyzeRydbergGate(telemetry);

        result.RydbergGateRegime.Should().Be("TWEEZER_POSITION_JITTER_DEPHASING_WARNING");
        result.IsGateFidelityHigh.Should().BeFalse();
        result.ControlGuidance.Should().ContainEquivalentOf("piezo-mirror acoustic stabilization");
    }

    [Fact]
    public void AnalyzeRydbergGate_ShortLifetime_ReturnsSubOptimalFidelity()
    {
        var telemetry = new NeutralAtomRydbergTelemetry(
            RegisterId: "RYD-REG-04",
            NeutralAtomQubitCount: 64,
            InteratomicSpacingMicrometers: 4.2,
            RydbergPrincipalQuantumNumberN: 60.0,
            LaserRabiFrequencyMegahertz: 2.0, // Slow gate ~ 3.14 us
            VanDerWaalsC6CoefficientGhzMicrometer6: 600.0,
            SpontaneousEmissionLifetimeMicroseconds: 25.0, // Short lifetime -> infidelity > 1.2%
            OpticalTweezerPositionJitterNanometers: 40.0
        );

        var result = _engine.AnalyzeRydbergGate(telemetry);

        result.RydbergGateRegime.Should().Be("SUB_OPTIMAL_GATE_FIDELITY_REGIME");
        result.IsGateFidelityHigh.Should().BeFalse();
        result.CalculatedTwoQubitGateFidelityPercent.Should().BeLessThan(99.0);
    }

    [Fact]
    public void AnalyzeRydbergGate_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeRydbergGate(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
