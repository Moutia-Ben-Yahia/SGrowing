using FluentAssertions;
using SGrowing.Api.Modules.Environmental.QclAtmosphericPlumeInversion;
using Xunit;

namespace SGrowing.Tests.Environmental;

public sealed class QclAtmosphericPlumeInversionTests
{
    private readonly QclAtmosphericPlumeInversionEngine _engine = new();

    [Fact]
    public void AnalyzeGreenhousePlume_CleanBackground_ReturnsAtmosphericNominal()
    {
        var telemetry = new QclPlumeTelemetry(
            SensorPathId: "QCL-PERMIAN-PATH-01",
            TargetMolecule: "CH4",
            OpticalPathLengthMeters: 1000.0,
            QclCenterWavelengthMicrons: 7.82,
            MeasuredPathIntegratedPpmMeters: 1950.0, // Clean baseline ~1.95 ppm
            CrosswindSpeedMetersPerSecond: 3.0,
            WindDirectionRelativeDegrees: 15.0,
            BeamScintillationRytovVariance: 0.05,
            RetroreflectorAlignmentTrackingLocked: true
        );

        var result = _engine.AnalyzeGreenhousePlume(telemetry);

        result.EmissionDetectionRegime.Should().Be("ATMOSPHERIC_BACKGROUND_NOMINAL");
        result.IsMeasurementValid.Should().BeTrue();
        result.PathAveragedMixingRatioPpm.Should().BeInRange(1.8, 2.1);
        result.InferredPlumeEmissionFluxKgPerHour.Should().BeLessThan(10.0);
    }

    [Fact]
    public void AnalyzeGreenhousePlume_SuperEmitterLeak_ReturnsSuperEmitterAlert()
    {
        var telemetry = new QclPlumeTelemetry(
            SensorPathId: "QCL-WELLPAD-LEAK-02",
            TargetMolecule: "CH4",
            OpticalPathLengthMeters: 1000.0,
            QclCenterWavelengthMicrons: 7.82,
            MeasuredPathIntegratedPpmMeters: 5200.0, // High plume absorption
            CrosswindSpeedMetersPerSecond: 3.5,
            WindDirectionRelativeDegrees: 10.0,
            BeamScintillationRytovVariance: 0.08,
            RetroreflectorAlignmentTrackingLocked: true
        );

        var result = _engine.AnalyzeGreenhousePlume(telemetry);

        result.EmissionDetectionRegime.Should().Be("FUGITIVE_EMISSION_SUPER_EMITTER_ALERT");
        result.IsMeasurementValid.Should().BeTrue();
        result.InferredPlumeEmissionFluxKgPerHour.Should().BeGreaterThan(25.0);
        result.PlumeLocalizationAction.Should().Contain("autonomous drone inspection");
    }

    [Fact]
    public void AnalyzeGreenhousePlume_HighScintillation_ReturnsBeamWanderHazard()
    {
        var telemetry = new QclPlumeTelemetry(
            SensorPathId: "QCL-TURBULENT-03",
            TargetMolecule: "CH4",
            OpticalPathLengthMeters: 1500.0,
            QclCenterWavelengthMicrons: 7.82,
            MeasuredPathIntegratedPpmMeters: 2500.0,
            CrosswindSpeedMetersPerSecond: 2.0,
            WindDirectionRelativeDegrees: 20.0,
            BeamScintillationRytovVariance: 0.55, // High Rytov variance > 0.40
            RetroreflectorAlignmentTrackingLocked: true
        );

        var result = _engine.AnalyzeGreenhousePlume(telemetry);

        result.EmissionDetectionRegime.Should().Be("OPTICAL_SCINTILLATION_BEAM_WANDER_HAZARD");
        result.IsMeasurementValid.Should().BeFalse();
        result.PlumeLocalizationAction.Should().Contain("piezo fast-steering mirror");
    }

    [Fact]
    public void AnalyzeGreenhousePlume_ParallelWind_ReturnsDispersionDecoupled()
    {
        var telemetry = new QclPlumeTelemetry(
            SensorPathId: "QCL-PARALLEL-WIND-04",
            TargetMolecule: "CH4",
            OpticalPathLengthMeters: 1200.0,
            QclCenterWavelengthMicrons: 7.82,
            MeasuredPathIntegratedPpmMeters: 3000.0,
            CrosswindSpeedMetersPerSecond: 4.0,
            WindDirectionRelativeDegrees: 84.0, // Parallel wind > 75 deg
            BeamScintillationRytovVariance: 0.10,
            RetroreflectorAlignmentTrackingLocked: true
        );

        var result = _engine.AnalyzeGreenhousePlume(telemetry);

        result.EmissionDetectionRegime.Should().Be("TRANSVERSE_WIND_DISPERSION_DECOUPLED");
        result.IsMeasurementValid.Should().BeFalse();
        result.PlumeLocalizationAction.Should().Contain("Reorient sensor link");
    }
}
