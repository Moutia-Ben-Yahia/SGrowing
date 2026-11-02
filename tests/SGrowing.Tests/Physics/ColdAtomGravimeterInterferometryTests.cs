namespace SGrowing.Tests.Physics;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Physics.ColdAtomGravimeterInterferometry;
using Xunit;

public class ColdAtomGravimeterInterferometryTests
{
    private readonly ColdAtomGravimeterInterferometryEngine _engine = new();

    [Fact]
    public void AnalyzeGravimeter_NominalColdCloud_ReturnsSubMicroGalPrecision()
    {
        var telemetry = new ColdAtomGravimeterTelemetry(
            GravimeterId: "GRAV-ATOM-01",
            Rb87AtomCloudCount: 2.0e6,
            InterrogationTimeTSeconds: 0.08,
            RamanLaserWavelengthNanometers: 780.24,
            RamanLaserChirpRateMhzPerSec: 25.13752, // Exact Doppler slope alpha = 2g / lambda
            ResidualSeismicVibrationRmsMicroG: 1.5,
            InterferometerInterferenceFringeContrastPercent: 55.0,
            EffectiveGravitationalAccelerationMps2: 9.80665
        );

        var result = _engine.AnalyzeGravimeter(telemetry);

        result.GravimeterId.Should().Be("GRAV-ATOM-01");
        result.GravimetryRegime.Should().Be("QUANTUM_SUB_MICROGAL_GRAVIMETRIC_PRECISION");
        result.IsMetrologyValid.Should().BeTrue();
        result.CalculatedGravitationalPhaseShiftRadians.Should().BeGreaterThan(900_000.0);
        result.GravityMeasurementResolutionMicroGal.Should().BeLessThan(3.0);
        result.ChirpRateDopplerErrorHertz.Should().BeLessThan(1000.0);
        result.FeedforwardVibrationControlGuidance.Should().ContainEquivalentOf("sub-microGal gravimetric sensitivity");
    }

    [Fact]
    public void AnalyzeGravimeter_LowContrastDecoherence_ReturnsFringeWashout()
    {
        var telemetry = new ColdAtomGravimeterTelemetry(
            GravimeterId: "GRAV-ATOM-02",
            Rb87AtomCloudCount: 8.0e5,
            InterrogationTimeTSeconds: 0.08,
            RamanLaserWavelengthNanometers: 780.24,
            RamanLaserChirpRateMhzPerSec: 25.136,
            ResidualSeismicVibrationRmsMicroG: 2.0,
            InterferometerInterferenceFringeContrastPercent: 11.5, // Collapsed contrast < 15%
            EffectiveGravitationalAccelerationMps2: 9.80665
        );

        var result = _engine.AnalyzeGravimeter(telemetry);

        result.GravimetryRegime.Should().Be("FRINGE_CONTRAST_WASHOUT_DECOHERENCE");
        result.IsMetrologyValid.Should().BeFalse();
        result.FeedforwardVibrationControlGuidance.Should().ContainEquivalentOf("sub-Doppler cooling");
    }

    [Fact]
    public void AnalyzeGravimeter_HighSeismicFloor_ReturnsSeismicWarning()
    {
        var telemetry = new ColdAtomGravimeterTelemetry(
            GravimeterId: "GRAV-ATOM-03",
            Rb87AtomCloudCount: 1.5e6,
            InterrogationTimeTSeconds: 0.08,
            RamanLaserWavelengthNanometers: 780.24,
            RamanLaserChirpRateMhzPerSec: 25.136,
            ResidualSeismicVibrationRmsMicroG: 22.5, // Exceeds 18 uG
            InterferometerInterferenceFringeContrastPercent: 50.0,
            EffectiveGravitationalAccelerationMps2: 9.80665
        );

        var result = _engine.AnalyzeGravimeter(telemetry);

        result.GravimetryRegime.Should().Be("SEISMIC_VIBRATION_DECORRELATION_WARNING");
        result.IsMetrologyValid.Should().BeFalse();
        result.FeedforwardVibrationControlGuidance.Should().ContainEquivalentOf("pneumatically isolated");
    }

    [Fact]
    public void AnalyzeGravimeter_ChirpDesynchronized_ReturnsChirpError()
    {
        var telemetry = new ColdAtomGravimeterTelemetry(
            GravimeterId: "GRAV-ATOM-04",
            Rb87AtomCloudCount: 1.5e6,
            InterrogationTimeTSeconds: 0.08,
            RamanLaserWavelengthNanometers: 780.24,
            RamanLaserChirpRateMhzPerSec: 25.142, // Error > 1500 Hz
            ResidualSeismicVibrationRmsMicroG: 2.0,
            InterferometerInterferenceFringeContrastPercent: 50.0,
            EffectiveGravitationalAccelerationMps2: 9.80665
        );

        var result = _engine.AnalyzeGravimeter(telemetry);

        result.GravimetryRegime.Should().Be("DOPPLER_CHIRP_DESYNCHRONIZATION");
        result.IsMetrologyValid.Should().BeFalse();
        result.ChirpRateDopplerErrorHertz.Should().BeGreaterThan(1500.0);
        result.FeedforwardVibrationControlGuidance.Should().ContainEquivalentOf("rubidium freefall rate");
    }

    [Fact]
    public void AnalyzeGravimeter_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeGravimeter(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
