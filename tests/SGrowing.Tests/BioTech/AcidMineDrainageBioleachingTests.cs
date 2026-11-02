namespace SGrowing.Tests.BioTech;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.BioTech.AcidMineDrainageBioleaching;
using Xunit;

public class AcidMineDrainageBioleachingTests
{
    private readonly AcidMineDrainageBioleachingEngine _engine = new();

    [Fact]
    public void AnalyzeBioleachingProcess_NominalOperatingConditions_ReturnsOptimalBioleaching()
    {
        var telemetry = new BioleachingTelemetry(
            BioreactorId: "BIO-HEAP-001",
            SolutionPh: 1.85,
            OxidationReductionPotentialMv: 720.0,
            FerricToFerrousRatio: 18.0,
            DissolvedOxygenMilligramsPerLiter: 5.5,
            TemperatureCelsius: 37.5,
            BacterialCellDensityCellsPerMl: 8.5e8,
            FeedChalcopyriteGradePercent: 2.8,
            SulfateIonConcentrationGramsPerLiter: 42.0
        );

        var result = _engine.AnalyzeBioleachingProcess(telemetry);

        result.BioreactorId.Should().Be("BIO-HEAP-001");
        result.BioleachingRegime.Should().Be("STEADY_STATE_CHEMOLITHOAUTOTROPHIC_BIOLEACHING");
        result.IsProcessOptimal.Should().BeTrue();
        result.BioleachingKineticsRateMgPerLiterPerHour.Should().BeGreaterThan(20.0);
        result.CumulativeCopperExtractionPercent.Should().BeGreaterThan(50.0);
        result.JarositePrecipitationRiskScore.Should().BeLessThan(0.40);
        result.AcidMineDrainageNeutralizationDemandKgLimePerM3.Should().BeGreaterThan(5.0);
        result.ProcessControlRecommendation.Should().ContainEquivalentOf("peak catalytic efficiency");
    }

    [Fact]
    public void AnalyzeBioleachingProcess_LowDissolvedOxygen_ReturnsAnoxicSuffocation()
    {
        var telemetry = new BioleachingTelemetry(
            BioreactorId: "BIO-HEAP-002",
            SolutionPh: 1.80,
            OxidationReductionPotentialMv: 680.0,
            FerricToFerrousRatio: 15.0,
            DissolvedOxygenMilligramsPerLiter: 0.8, // Critical DO deficiency
            TemperatureCelsius: 36.0,
            BacterialCellDensityCellsPerMl: 5.0e8,
            FeedChalcopyriteGradePercent: 2.5,
            SulfateIonConcentrationGramsPerLiter: 40.0
        );

        var result = _engine.AnalyzeBioleachingProcess(telemetry);

        result.BioleachingRegime.Should().Be("ANOXIC_BACTERIAL_SUFFOCATION");
        result.IsProcessOptimal.Should().BeFalse();
        result.ProcessControlRecommendation.Should().ContainEquivalentOf("sparger aeration rate");
    }

    [Fact]
    public void AnalyzeBioleachingProcess_ElevatedPhAndSulfate_ReturnsJarositeHazard()
    {
        var telemetry = new BioleachingTelemetry(
            BioreactorId: "BIO-HEAP-003",
            SolutionPh: 2.45, // High pH triggers jarosite
            OxidationReductionPotentialMv: 710.0,
            FerricToFerrousRatio: 35.0,
            DissolvedOxygenMilligramsPerLiter: 4.8,
            TemperatureCelsius: 38.0,
            BacterialCellDensityCellsPerMl: 6.0e8,
            FeedChalcopyriteGradePercent: 2.2,
            SulfateIonConcentrationGramsPerLiter: 75.0
        );

        var result = _engine.AnalyzeBioleachingProcess(telemetry);

        result.BioleachingRegime.Should().Be("JAROSITE_SURFACE_PASSIVATION_HAZARD");
        result.IsProcessOptimal.Should().BeFalse();
        result.JarositePrecipitationRiskScore.Should().BeGreaterThan(0.50);
        result.ProcessControlRecommendation.Should().ContainEquivalentOf("sulfuric acid");
    }

    [Fact]
    public void AnalyzeBioleachingProcess_LowRedoxPotential_ReturnsSuboptimalRedox()
    {
        var telemetry = new BioleachingTelemetry(
            BioreactorId: "BIO-HEAP-004",
            SolutionPh: 1.90,
            OxidationReductionPotentialMv: 490.0, // Low Eh < 550 mV
            FerricToFerrousRatio: 2.1,
            DissolvedOxygenMilligramsPerLiter: 4.0,
            TemperatureCelsius: 35.0,
            BacterialCellDensityCellsPerMl: 1.0e7,
            FeedChalcopyriteGradePercent: 1.8,
            SulfateIonConcentrationGramsPerLiter: 35.0
        );

        var result = _engine.AnalyzeBioleachingProcess(telemetry);

        result.BioleachingRegime.Should().Be("SUBOPTIMAL_REDOX_POTENTIAL");
        result.IsProcessOptimal.Should().BeFalse();
        result.ProcessControlRecommendation.Should().ContainEquivalentOf("Acidithiobacillus");
    }

    [Fact]
    public void AnalyzeBioleachingProcess_ExtremeTemperature_ReturnsThermalInhibition()
    {
        var telemetry = new BioleachingTelemetry(
            BioreactorId: "BIO-HEAP-005",
            SolutionPh: 1.85,
            OxidationReductionPotentialMv: 690.0,
            FerricToFerrousRatio: 12.0,
            DissolvedOxygenMilligramsPerLiter: 5.0,
            TemperatureCelsius: 56.0, // Exceeds 52 C
            BacterialCellDensityCellsPerMl: 4.0e8,
            FeedChalcopyriteGradePercent: 2.0,
            SulfateIonConcentrationGramsPerLiter: 38.0
        );

        var result = _engine.AnalyzeBioleachingProcess(telemetry);

        result.BioleachingRegime.Should().Be("THERMAL_MICROBIAL_INHIBITION");
        result.IsProcessOptimal.Should().BeFalse();
        result.ProcessControlRecommendation.Should().ContainEquivalentOf("cooling/heating jacket");
    }

    [Fact]
    public void AnalyzeBioleachingProcess_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeBioleachingProcess(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
