using FluentAssertions;
using SGrowing.Api.Modules.Energy.NeutrinoGeotomographyCoreDensity;
using Xunit;

namespace SGrowing.Tests.Energy;

public sealed class NeutrinoGeotomographyCoreDensityTests
{
    private readonly NeutrinoGeotomographyCoreDensityEngine _engine = new();

    [Fact]
    public void AnalyzePlanetaryInterior_NominalEarthCoreChord_ReturnsPremConsistent()
    {
        var telemetry = new NeutrinoTomographyTelemetry(
            TomographyArrayId: "ICECUBE-KM3NET-01",
            NeutrinoEnergyTeV: 10.0,
            EarthChordNadirAngleDegrees: 0.0, // Through Earth center
            MeasuredNeutrinoTransmittance: 0.28,
            MeasuredGeoAntineutrinoFluxTnu: 48.0,
            ThoriumToUraniumMassRatio: 3.9,
            InferredGeoReactorPowerTerawatts: 1.2,
            MatterEnhancedOscillationMswCorrected: true
        );

        var result = _engine.AnalyzePlanetaryInterior(telemetry);

        result.PlanetaryGeophysicsRegime.Should().Be("PREM_CORE_MANTLE_CONSISTENT");
        result.IsPlanetaryModelConsistent.Should().BeTrue();
        result.InferredAverageChordDensityGramsPerCm3.Should().BeInRange(8.0, 12.0);
        result.RadiogenicHeatProductionTerawatts.Should().BeGreaterThan(15.0);
        result.CoreMantleBoundaryDensityJumpRatio.Should().BeGreaterThan(1.50);
    }

    [Fact]
    public void AnalyzePlanetaryInterior_HighGeoReactorFlux_ReturnsSurgeDetected()
    {
        var telemetry = new NeutrinoTomographyTelemetry(
            TomographyArrayId: "BOREXINO-KAMLAND-02",
            NeutrinoEnergyTeV: 10.0,
            EarthChordNadirAngleDegrees: 25.0,
            MeasuredNeutrinoTransmittance: 0.32,
            MeasuredGeoAntineutrinoFluxTnu: 52.0,
            ThoriumToUraniumMassRatio: 3.8,
            InferredGeoReactorPowerTerawatts: 4.5, // Exceeds 3.5 TW global empirical limit
            MatterEnhancedOscillationMswCorrected: true
        );

        var result = _engine.AnalyzePlanetaryInterior(telemetry);

        result.PlanetaryGeophysicsRegime.Should().Be("GEO_REACTOR_POWER_SURGE_DETECTED");
        result.IsPlanetaryModelConsistent.Should().BeFalse();
        result.GeophysicalInterpretation.Should().Contain("natural nuclear fission anomaly");
    }

    [Fact]
    public void AnalyzePlanetaryInterior_UltraDenseCoreChord_ReturnsHeavyElementAnomaly()
    {
        var telemetry = new NeutrinoTomographyTelemetry(
            TomographyArrayId: "HYPER-K-DENSE-03",
            NeutrinoEnergyTeV: 10.0,
            EarthChordNadirAngleDegrees: 5.0,
            MeasuredNeutrinoTransmittance: 0.10, // Extreme absorption -> tau = 2.30 -> density = 18.2 g/cm3 > 14.5
            MeasuredGeoAntineutrinoFluxTnu: 45.0,
            ThoriumToUraniumMassRatio: 4.0,
            InferredGeoReactorPowerTerawatts: 1.0,
            MatterEnhancedOscillationMswCorrected: true
        );

        var result = _engine.AnalyzePlanetaryInterior(telemetry);

        result.PlanetaryGeophysicsRegime.Should().Be("HEAVY_ELEMENT_CORE_ANOMALY");
        result.IsPlanetaryModelConsistent.Should().BeFalse();
        result.InferredAverageChordDensityGramsPerCm3.Should().BeGreaterThan(14.5);
    }

    [Fact]
    public void AnalyzePlanetaryInterior_DepletedCrustFlux_ReturnsDepletionWarning()
    {
        var telemetry = new NeutrinoTomographyTelemetry(
            TomographyArrayId: "SNO-PLUS-DEPLETED-04",
            NeutrinoEnergyTeV: 10.0,
            EarthChordNadirAngleDegrees: 60.0, // Mantle-only chord
            MeasuredNeutrinoTransmittance: 0.70,
            MeasuredGeoAntineutrinoFluxTnu: 18.0, // Low flux -> 18 * 0.42 = 7.56 TW < 10.0
            ThoriumToUraniumMassRatio: 1.8,  // Anomalous ratio < 2.2
            InferredGeoReactorPowerTerawatts: 0.2,
            MatterEnhancedOscillationMswCorrected: true
        );

        var result = _engine.AnalyzePlanetaryInterior(telemetry);

        result.PlanetaryGeophysicsRegime.Should().Be("CRUSTAL_RADIOACTIVITY_DEPLETION");
        result.IsPlanetaryModelConsistent.Should().BeFalse();
        result.RadiogenicHeatProductionTerawatts.Should().BeLessThan(10.0);
    }
}
