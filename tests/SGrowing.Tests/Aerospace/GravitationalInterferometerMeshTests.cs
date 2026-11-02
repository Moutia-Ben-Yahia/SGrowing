namespace SGrowing.Tests.Aerospace;

using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.GravitationalInterferometerMesh;
using Xunit;

public class GravitationalInterferometerMeshTests
{
    private readonly GravitationalInterferometerMeshEngine _engine = new();

    [Fact]
    public void SynthesizeTdi_InsufficientTelemetry_ReturnsEmptyChannels()
    {
        var request = new TdiSynthesisRequest("LISA-TEST", ArmMeasurements: []);
        var result = _engine.SynthesizeTdiAndDetectStrain(request);

        result.AstrophysicalSignalDetected.Should().BeFalse();
        result.TdiChannels.Should().BeEmpty();
        result.SourceCandidateType.Should().Be("INSUFFICIENT_TELEMETRY");
    }

    [Fact]
    public void SynthesizeTdi_SymmetricNoiseCancelsOut_ReportsInstrumentalNoiseFloor()
    {
        // Common laser phase fluctuations across symmetric arms cancel out in TDI combinations
        var measurements = new List<InterferometerArmMeasurement>
        {
            new("ARM-12", 1, 2, 0.0001, 1.2),
            new("ARM-21", 2, 1, 0.0001, 1.2),
            new("ARM-13", 1, 3, 0.0001, 1.2),
            new("ARM-31", 3, 1, 0.0001, 1.2),
            new("ARM-23", 2, 3, 0.0001, 1.2),
            new("ARM-32", 3, 2, 0.0001, 1.2)
        };

        var request = new TdiSynthesisRequest("LISA-NOMINAL", 2.5e9, 281.6e12, measurements);
        var result = _engine.SynthesizeTdiAndDetectStrain(request);

        result.AstrophysicalSignalDetected.Should().BeFalse();
        result.SignalToNoiseRatio.Should().BeLessThan(7.0);
        result.SourceCandidateType.Should().Be("INSTRUMENTAL_NOISE_FLOOR");
        result.TdiChannels.Should().HaveCount(4);
    }

    [Fact]
    public void SynthesizeTdi_BlackHoleMergerSignal_DetectsAstrophysicalStrainAboveThreshold()
    {
        // Differential phase perturbation induced by passing quadrupole gravitational wave: Delta_phi ~ 2.5e-4 rad
        var measurements = new List<InterferometerArmMeasurement>
        {
            new("ARM-12", 1, 2, 2.5e-4, 1.5),
            new("ARM-21", 2, 1, 0.0, 1.5),
            new("ARM-13", 1, 3, -2.5e-4, 1.5),
            new("ARM-31", 3, 1, 0.0, 1.5),
            new("ARM-23", 2, 3, 0.0, 1.5),
            new("ARM-32", 3, 2, 0.0, 1.5)
        };

        var request = new TdiSynthesisRequest("LISA-OBSERVATION", 2.5e9, 281.6e12, measurements);
        var result = _engine.SynthesizeTdiAndDetectStrain(request);

        result.AstrophysicalSignalDetected.Should().BeTrue();
        result.SignalToNoiseRatio.Should().BeGreaterThan(7.0);
        result.SourceCandidateType.Should().NotBe("INSTRUMENTAL_NOISE_FLOOR");
        result.ReconstructedStrainAmplitudeH.Should().BeGreaterThan(1e-21);
    }

    [Fact]
    public void SynthesizeTdi_GeneratesExpectedChannels_WithHighNoiseCancellationDb()
    {
        var measurements = new List<InterferometerArmMeasurement>
        {
            new("ARM-12", 1, 2, 0.002, 1.0),
            new("ARM-21", 2, 1, 0.001, 1.0),
            new("ARM-13", 1, 3, 0.003, 1.0),
            new("ARM-31", 3, 1, 0.002, 1.0),
            new("ARM-23", 2, 3, 0.001, 1.0),
            new("ARM-32", 3, 2, 0.002, 1.0)
        };

        var request = new TdiSynthesisRequest("LISA-CALIBRATION", 2.5e9, 281.6e12, measurements);
        var result = _engine.SynthesizeTdiAndDetectStrain(request);

        result.TdiChannels.Should().Contain(c => c.ChannelName == "TDI_X" && c.LaserPhaseNoiseCancellationDb >= 160.0);
        result.TdiChannels.Should().Contain(c => c.ChannelName == "Sagnac_alpha");
    }
}
