namespace SGrowing.Tests.BioTech;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.BioTech.EpigeneticMethylationClock;
using Xunit;

public class EpigeneticMethylationClockTests
{
    private readonly EpigeneticMethylationClockEngine _engine = new();

    [Fact]
    public void AnalyzeEpigeneticClock_NominalProfile_ReturnsChromatinHomeostasis()
    {
        var telemetry = new EpigeneticClockTelemetry(
            SampleId: "SAMPLE-EPI-01",
            ChronologicalAgeYears: 42.0,
            MeanCpGIslandMethylationBeta: 0.48,
            TumorSuppressorPromoterHypermethylationPercent: 12.0,
            Line1RetrotransposonMethylationPercent: 82.0,
            HistoneAcetylationH3K27acRatio: 1.25,
            HdacEnzymeActivityUnits: 14.5,
            AnalyzedSingleCellCount: 15_000
        );

        var result = _engine.AnalyzeEpigeneticClock(telemetry);

        result.SampleId.Should().Be("SAMPLE-EPI-01");
        result.EpigeneticRegime.Should().Be("REJUVENATED_CHROMATIN_HOMEOSTASIS");
        result.IsEpigeneticProfileOptimal.Should().BeTrue();
        result.EpigeneticAgeAccelerationYears.Should().BeLessThan(5.0);
        result.GenomicStabilityIndex.Should().BeGreaterThan(0.70);
        result.TherapeuticEpigeneticGuidance.Should().ContainEquivalentOf("Chromatin architecture and histone acetylation optimal");
    }

    [Fact]
    public void AnalyzeEpigeneticClock_TumorSuppressorHypermethylation_ReturnsMalignancyHazard()
    {
        var telemetry = new EpigeneticClockTelemetry(
            SampleId: "SAMPLE-EPI-02",
            ChronologicalAgeYears: 55.0,
            MeanCpGIslandMethylationBeta: 0.65,
            TumorSuppressorPromoterHypermethylationPercent: 52.0, // Exceeds 45%
            Line1RetrotransposonMethylationPercent: 70.0,
            HistoneAcetylationH3K27acRatio: 0.8,
            HdacEnzymeActivityUnits: 22.0,
            AnalyzedSingleCellCount: 8_000
        );

        var result = _engine.AnalyzeEpigeneticClock(telemetry);

        result.EpigeneticRegime.Should().Be("EPIGENETIC_MALIGNANCY_SILENCING_HAZARD");
        result.IsEpigeneticProfileOptimal.Should().BeFalse();
        result.TherapeuticEpigeneticGuidance.Should().ContainEquivalentOf("DNA methyltransferase (DNMT) inhibitor");
    }

    [Fact]
    public void AnalyzeEpigeneticClock_Line1Hypomethylation_ReturnsGenomeInstability()
    {
        var telemetry = new EpigeneticClockTelemetry(
            SampleId: "SAMPLE-EPI-03",
            ChronologicalAgeYears: 48.0,
            MeanCpGIslandMethylationBeta: 0.45,
            TumorSuppressorPromoterHypermethylationPercent: 18.0,
            Line1RetrotransposonMethylationPercent: 44.0, // Hypomethylated < 55%
            HistoneAcetylationH3K27acRatio: 1.0,
            HdacEnzymeActivityUnits: 15.0,
            AnalyzedSingleCellCount: 12_000
        );

        var result = _engine.AnalyzeEpigeneticClock(telemetry);

        result.EpigeneticRegime.Should().Be("GENOME_HYPOMETHYLATION_INSTABILITY");
        result.IsEpigeneticProfileOptimal.Should().BeFalse();
        result.TherapeuticEpigeneticGuidance.Should().ContainEquivalentOf("LINE-1 retrotransposons");
    }

    [Fact]
    public void AnalyzeEpigeneticClock_AcceleratedAging_ReturnsSenescenceRegime()
    {
        var telemetry = new EpigeneticClockTelemetry(
            SampleId: "SAMPLE-EPI-04",
            ChronologicalAgeYears: 40.0,
            MeanCpGIslandMethylationBeta: 0.85, // High CpG methylation
            TumorSuppressorPromoterHypermethylationPercent: 25.0,
            Line1RetrotransposonMethylationPercent: 72.0,
            HistoneAcetylationH3K27acRatio: 0.7,
            HdacEnzymeActivityUnits: 36.0, // High HDAC activity
            AnalyzedSingleCellCount: 10_000
        );

        var result = _engine.AnalyzeEpigeneticClock(telemetry);

        result.EpigeneticRegime.Should().Be("ACCELERATED_EPIGENETIC_SENESCENCE");
        result.IsEpigeneticProfileOptimal.Should().BeFalse();
        result.EpigeneticAgeAccelerationYears.Should().BeGreaterThan(8.0);
        result.TherapeuticEpigeneticGuidance.Should().ContainEquivalentOf("selective HDAC inhibitor");
    }

    [Fact]
    public void AnalyzeEpigeneticClock_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeEpigeneticClock(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
