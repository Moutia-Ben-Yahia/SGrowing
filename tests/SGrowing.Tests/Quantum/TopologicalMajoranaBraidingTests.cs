namespace SGrowing.Tests.Quantum;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Quantum.TopologicalMajoranaBraiding;
using Xunit;

public class TopologicalMajoranaBraidingTests
{
    private readonly TopologicalMajoranaBraidingEngine _engine = new();

    [Fact]
    public void AnalyzeMajoranaBraiding_NominalTopologicalPhase_ReturnsProtectedBraiding()
    {
        var telemetry = new TopologicalMajoranaTelemetry(
            QubitJunctionId: "MZM-TOPO-01",
            NanowireLengthMicrometers: 1.8, // 1800 nm -> L/xi = 10
            MajoranaLocalizationLengthNanometers: 180.0,
            AppliedZeemanMagneticFieldTesla: 0.85,
            CriticalTopologicalFieldThresholdTesla: 0.40, // B > B_c
            TopologicalSuperconductingGapMicroElectronVolts: 120.0,
            BraidingOperationDurationNanoseconds: 45.0,
            QuasiparticlePoisoningTimeMicroseconds: 25.0,
            CryogenicBaseTemperatureMilliKelvin: 20.0 // 20 mK
        );

        var result = _engine.AnalyzeMajoranaBraiding(telemetry);

        result.QubitJunctionId.Should().Be("MZM-TOPO-01");
        result.TopologicalQuantumRegime.Should().Be("TOPOLOGICALLY_PROTECTED_NON_ABELIAN_BRAIDING");
        result.IsBraidingOperationFaultTolerant.Should().BeTrue();
        result.MajoranaWavefunctionOverlapSplittingMicroEv.Should().BeLessThan(0.01); // Negligible overlap
        result.AdiabaticBraidingFidelityPercent.Should().BeGreaterThan(99.0);
        result.ParityProtectionLifetimeMicroseconds.Should().BeGreaterThan(20.0);
        result.BraidingControlGuidance.Should().ContainEquivalentOf("topologically protected against local decoherence");
    }

    [Fact]
    public void AnalyzeMajoranaBraiding_BelowCriticalField_ReturnsTrivialPhase()
    {
        var telemetry = new TopologicalMajoranaTelemetry(
            QubitJunctionId: "MZM-TOPO-02",
            NanowireLengthMicrometers: 1.8,
            MajoranaLocalizationLengthNanometers: 180.0,
            AppliedZeemanMagneticFieldTesla: 0.25, // B < B_c = 0.40
            CriticalTopologicalFieldThresholdTesla: 0.40,
            TopologicalSuperconductingGapMicroElectronVolts: 120.0,
            BraidingOperationDurationNanoseconds: 45.0,
            QuasiparticlePoisoningTimeMicroseconds: 25.0,
            CryogenicBaseTemperatureMilliKelvin: 20.0
        );

        var result = _engine.AnalyzeMajoranaBraiding(telemetry);

        result.TopologicalQuantumRegime.Should().Be("TRIVIAL_SUPERCONDUCTING_PHASE_NO_MZMS");
        result.IsBraidingOperationFaultTolerant.Should().BeFalse();
        result.BraidingControlGuidance.Should().ContainEquivalentOf("trivial s-wave superconducting phase");
    }

    [Fact]
    public void AnalyzeMajoranaBraiding_ShortNanowire_ReturnsHybridizationCollapse()
    {
        var telemetry = new TopologicalMajoranaTelemetry(
            QubitJunctionId: "MZM-TOPO-03",
            NanowireLengthMicrometers: 0.50, // 500 nm -> L/xi = 2.77 < 5
            MajoranaLocalizationLengthNanometers: 180.0,
            AppliedZeemanMagneticFieldTesla: 0.85,
            CriticalTopologicalFieldThresholdTesla: 0.40,
            TopologicalSuperconductingGapMicroElectronVolts: 120.0,
            BraidingOperationDurationNanoseconds: 45.0,
            QuasiparticlePoisoningTimeMicroseconds: 25.0,
            CryogenicBaseTemperatureMilliKelvin: 20.0
        );

        var result = _engine.AnalyzeMajoranaBraiding(telemetry);

        result.TopologicalQuantumRegime.Should().Be("MAJORANA_OVERLAP_HYBRIDIZATION_COLLAPSE");
        result.IsBraidingOperationFaultTolerant.Should().BeFalse();
        result.BraidingControlGuidance.Should().ContainEquivalentOf("Use longer nanowires");
    }

    [Fact]
    public void AnalyzeMajoranaBraiding_HighTemperature_ReturnsQuasiparticleHazard()
    {
        var telemetry = new TopologicalMajoranaTelemetry(
            QubitJunctionId: "MZM-TOPO-04",
            NanowireLengthMicrometers: 1.8,
            MajoranaLocalizationLengthNanometers: 180.0,
            AppliedZeemanMagneticFieldTesla: 0.85,
            CriticalTopologicalFieldThresholdTesla: 0.40,
            TopologicalSuperconductingGapMicroElectronVolts: 120.0,
            BraidingOperationDurationNanoseconds: 45.0,
            QuasiparticlePoisoningTimeMicroseconds: 25.0,
            CryogenicBaseTemperatureMilliKelvin: 220.0 // Exceeds 150 mK
        );

        var result = _engine.AnalyzeMajoranaBraiding(telemetry);

        result.TopologicalQuantumRegime.Should().Be("THERMAL_QUASIPARTICLE_POISONING_HAZARD");
        result.IsBraidingOperationFaultTolerant.Should().BeFalse();
        result.BraidingControlGuidance.Should().ContainEquivalentOf("Chill dilution refrigerator");
    }

    [Fact]
    public void AnalyzeMajoranaBraiding_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeMajoranaBraiding(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
