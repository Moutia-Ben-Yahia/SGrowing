namespace SGrowing.Tests.BioTech;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.BioTech.IvtMrnaCappingPolyATail;
using Xunit;

public class IvtMrnaCappingPolyATailTests
{
    private readonly IvtMrnaCappingPolyATailEngine _engine = new();

    [Fact]
    public void AnalyzeIvtMrnaBatch_NominalQuality_ReturnsHighFidelitySynthesis()
    {
        var telemetry = new IvtMrnaTelemetry(
            BatchId: "MRNA-BATCH-01",
            CleanCapIncorporationEfficiencyPercent: 97.5,
            DoubleStrandedRnaContaminationPercent: 0.18,
            MeanPolyATailLengthNucleotides: 125.0,
            PolyATailHeterogeneityIndex: 0.25,
            TotalTranscriptLengthNucleotides: 1850.0,
            PostPurificationYieldPercent: 88.0,
            InVitroTranslationEfficiencyScore: 9.2
        );

        var result = _engine.AnalyzeIvtMrnaBatch(telemetry);

        result.BatchId.Should().Be("MRNA-BATCH-01");
        result.SynthesisQualityRegime.Should().Be("HIGH_FIDELITY_THERAPEUTIC_MRNA_SYNTHESIS");
        result.IsMrnaTherapeuticallyViable.Should().BeTrue();
        result.InnateImmuneImmunogenicityScore.Should().BeLessThan(2.0);
        result.PredictedCytosolicHalfLifeHours.Should().BeGreaterThan(12.0);
        result.NetEffectiveTranslationIndex.Should().BeGreaterThan(7.5);
        result.UpstreamAndDownstreamGuidance.Should().ContainEquivalentOf("Translational competence and biocompatibility assured");
    }

    [Fact]
    public void AnalyzeIvtMrnaBatch_HighDsRnaImpurity_ReturnsPkrShutdown()
    {
        var telemetry = new IvtMrnaTelemetry(
            BatchId: "MRNA-BATCH-02",
            CleanCapIncorporationEfficiencyPercent: 96.0,
            DoubleStrandedRnaContaminationPercent: 1.85, // Exceeds 1.2%
            MeanPolyATailLengthNucleotides: 120.0,
            PolyATailHeterogeneityIndex: 0.30,
            TotalTranscriptLengthNucleotides: 1850.0,
            PostPurificationYieldPercent: 82.0,
            InVitroTranslationEfficiencyScore: 8.5
        );

        var result = _engine.AnalyzeIvtMrnaBatch(telemetry);

        result.SynthesisQualityRegime.Should().Be("DSRNA_IMMUNOGENIC_PKR_SHUTDOWN");
        result.IsMrnaTherapeuticallyViable.Should().BeFalse();
        result.UpstreamAndDownstreamGuidance.Should().ContainEquivalentOf("cellulose/HPLC polishing");
    }

    [Fact]
    public void AnalyzeIvtMrnaBatch_UncappedContamination_ReturnsRigImmuneHazard()
    {
        var telemetry = new IvtMrnaTelemetry(
            BatchId: "MRNA-BATCH-03",
            CleanCapIncorporationEfficiencyPercent: 87.5, // Capping < 92%
            DoubleStrandedRnaContaminationPercent: 0.25,
            MeanPolyATailLengthNucleotides: 115.0,
            PolyATailHeterogeneityIndex: 0.20,
            TotalTranscriptLengthNucleotides: 1850.0,
            PostPurificationYieldPercent: 90.0,
            InVitroTranslationEfficiencyScore: 8.0
        );

        var result = _engine.AnalyzeIvtMrnaBatch(telemetry);

        result.SynthesisQualityRegime.Should().Be("UNCAPPED_TRIPHOSPHATE_RIG_IMMUNE_HAZARD");
        result.IsMrnaTherapeuticallyViable.Should().BeFalse();
        result.UpstreamAndDownstreamGuidance.Should().ContainEquivalentOf("RIG-I pattern recognition");
    }

    [Fact]
    public void AnalyzeIvtMrnaBatch_ShortPolyATail_ReturnsTranslationalInstability()
    {
        var telemetry = new IvtMrnaTelemetry(
            BatchId: "MRNA-BATCH-04",
            CleanCapIncorporationEfficiencyPercent: 96.5,
            DoubleStrandedRnaContaminationPercent: 0.20,
            MeanPolyATailLengthNucleotides: 65.0, // Depleted < 85 nt
            PolyATailHeterogeneityIndex: 0.80,
            TotalTranscriptLengthNucleotides: 1850.0,
            PostPurificationYieldPercent: 85.0,
            InVitroTranslationEfficiencyScore: 7.8
        );

        var result = _engine.AnalyzeIvtMrnaBatch(telemetry);

        result.SynthesisQualityRegime.Should().Be("SHORT_POLYA_TRANSLATIONAL_INSTABILITY");
        result.IsMrnaTherapeuticallyViable.Should().BeFalse();
        result.UpstreamAndDownstreamGuidance.Should().ContainEquivalentOf("Premature deadenylation");
    }

    [Fact]
    public void AnalyzeIvtMrnaBatch_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeIvtMrnaBatch(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
