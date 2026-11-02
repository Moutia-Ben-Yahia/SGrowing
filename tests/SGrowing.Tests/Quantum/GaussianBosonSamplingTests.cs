namespace SGrowing.Tests.Quantum;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Quantum.GaussianBosonSampling;
using Xunit;

public class GaussianBosonSamplingTests
{
    private readonly GaussianBosonSamplingEngine _engine = new();

    [Fact]
    public void AnalyzeGaussianBosonSampling_HighPhotonCountAdvantage_ReturnsAdvantageVerified()
    {
        var telemetry = new GbsTelemetry(
            InterferometerId: "PHOTONIC-QPU-01",
            OpticalModeCount: 100,
            MeanSqueezingParameterR: 1.45,
            PhotonIndistinguishabilityVisibility: 0.985,
            OverallOpticalTransmissionEfficiency: 0.84,
            DetectedOutputPhotonCount: 76,
            ClassicalMcmcSampleSimilarityScore: 0.08,
            InterferometerPhaseStabilityRmsRadians: 0.025
        );

        var result = _engine.AnalyzeGaussianBosonSampling(telemetry);

        result.InterferometerId.Should().Be("PHOTONIC-QPU-01");
        result.SamplingRegime.Should().Be("QUANTUM_COMPUTATIONAL_ADVANTAGE_VERIFIED");
        result.IsQuantumAdvantageCertified.Should().BeTrue();
        result.TorontonianMatrixDimension.Should().Be(152);
        result.Log10ClassicalHafnianSimulationComplexity.Should().BeGreaterThan(15.0);
        result.QuantumComputationalSupremacyMargin.Should().BeGreaterThan(10.0);
        result.OpticalMeshCalibrationAction.Should().ContainEquivalentOf("Quantum advantage certified");
    }

    [Fact]
    public void AnalyzeGaussianBosonSampling_HighPhaseJitter_ReturnsPhaseNoiseHazard()
    {
        var telemetry = new GbsTelemetry(
            InterferometerId: "PHOTONIC-QPU-02",
            OpticalModeCount: 80,
            MeanSqueezingParameterR: 1.30,
            PhotonIndistinguishabilityVisibility: 0.98,
            OverallOpticalTransmissionEfficiency: 0.80,
            DetectedOutputPhotonCount: 65,
            ClassicalMcmcSampleSimilarityScore: 0.12,
            InterferometerPhaseStabilityRmsRadians: 0.11 // Exceeds 0.08 rad
        );

        var result = _engine.AnalyzeGaussianBosonSampling(telemetry);

        result.SamplingRegime.Should().Be("PHASE_NOISE_DECOHERENCE_HAZARD");
        result.IsQuantumAdvantageCertified.Should().BeFalse();
        result.OpticalMeshCalibrationAction.Should().ContainEquivalentOf("active piezo-mirror phase locking");
    }

    [Fact]
    public void AnalyzeGaussianBosonSampling_HighMcmcSimilarity_ReturnsClassicalMockDistribution()
    {
        var telemetry = new GbsTelemetry(
            InterferometerId: "PHOTONIC-QPU-03",
            OpticalModeCount: 60,
            MeanSqueezingParameterR: 0.95,
            PhotonIndistinguishabilityVisibility: 0.97,
            OverallOpticalTransmissionEfficiency: 0.78,
            DetectedOutputPhotonCount: 55,
            ClassicalMcmcSampleSimilarityScore: 0.52, // Exceeds 0.40
            InterferometerPhaseStabilityRmsRadians: 0.03
        );

        var result = _engine.AnalyzeGaussianBosonSampling(telemetry);

        result.SamplingRegime.Should().Be("CLASSICAL_MOCK_SIMULABLE_DISTRIBUTION");
        result.IsQuantumAdvantageCertified.Should().BeFalse();
        result.OpticalMeshCalibrationAction.Should().ContainEquivalentOf("spoofable by classical thermal");
    }

    [Fact]
    public void AnalyzeGaussianBosonSampling_TransmissionLossLimited_ReturnsOpticalLossRegime()
    {
        var telemetry = new GbsTelemetry(
            InterferometerId: "PHOTONIC-QPU-04",
            OpticalModeCount: 80,
            MeanSqueezingParameterR: 1.2,
            PhotonIndistinguishabilityVisibility: 0.92, // Visibility < 0.96
            OverallOpticalTransmissionEfficiency: 0.62, // Transmission < 0.70
            DetectedOutputPhotonCount: 50,
            ClassicalMcmcSampleSimilarityScore: 0.15,
            InterferometerPhaseStabilityRmsRadians: 0.03
        );

        var result = _engine.AnalyzeGaussianBosonSampling(telemetry);

        result.SamplingRegime.Should().Be("OPTICAL_TRANSMISSION_LOSS_LIMITED");
        result.IsQuantumAdvantageCertified.Should().BeFalse();
        result.OpticalMeshCalibrationAction.Should().ContainEquivalentOf("Hong-Ou-Mandel");
    }

    [Fact]
    public void AnalyzeGaussianBosonSampling_SubAdvantagePhotonCount_ReturnsBenchmarkingRegime()
    {
        var telemetry = new GbsTelemetry(
            InterferometerId: "PHOTONIC-QPU-05",
            OpticalModeCount: 50,
            MeanSqueezingParameterR: 1.0,
            PhotonIndistinguishabilityVisibility: 0.98,
            OverallOpticalTransmissionEfficiency: 0.85,
            DetectedOutputPhotonCount: 28, // Low photon count < 45
            ClassicalMcmcSampleSimilarityScore: 0.10,
            InterferometerPhaseStabilityRmsRadians: 0.02
        );

        var result = _engine.AnalyzeGaussianBosonSampling(telemetry);

        result.SamplingRegime.Should().Be("SUB_ADVANTAGE_BENCHMARKING_REGIME");
        result.IsQuantumAdvantageCertified.Should().BeFalse();
        result.OpticalMeshCalibrationAction.Should().ContainEquivalentOf("below computational advantage regime");
    }

    [Fact]
    public void AnalyzeGaussianBosonSampling_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeGaussianBosonSampling(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
