namespace SGrowing.Tests.BioTech;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.BioTech.PrimeEditingSearchAndReplace;
using Xunit;

public class PrimeEditingSearchAndReplaceTests
{
    private readonly PrimeEditingSearchAndReplaceEngine _engine = new();

    [Fact]
    public void AnalyzePrimeEditing_NominalPe3_ReturnsHighPrecisionRegime()
    {
        var telemetry = new PrimeEditingTelemetry(
            EditTargetLocusId: "HBB-SICKLE-E6V",
            PrimerBindingSiteLengthNucleotides: 14,
            ReverseTranscriptionTemplateLengthNucleotides: 16,
            PbsMeltingTemperatureCelsius: 38.5,
            RttGcContentPercent: 50.0,
            IsPe3NonEditedStrandNickApplied: true,
            NonEditedStrandNickDistanceBasePairs: 52.0, // Optimal PE3 window
            SecondaryStructureFreeEnergyDeltaG: -2.1,
            TargetGenomicLocusAccessibilityScore: 0.90
        );

        var result = _engine.AnalyzePrimeEditing(telemetry);

        result.EditTargetLocusId.Should().Be("HBB-SICKLE-E6V");
        result.PrimeEditingRegime.Should().Be("HIGH_PRECISION_SEARCH_AND_REPLACE_REGIME");
        result.IsEditingPrecisionHigh.Should().BeTrue();
        result.PrimeEditingSystemType.Should().Be("PE3_DUAL_NICKING_SEARCH_AND_REPLACE");
        result.PredictedOnTargetEditingEfficiencyPercent.Should().BeGreaterThan(50.0);
        result.PredictedIndelFormationRatePercent.Should().BeLessThan(2.0);
        result.EditToIndelPrecisionRatio.Should().BeGreaterThan(30.0);
        result.PegRnaOptimizationGuidance.Should().ContainEquivalentOf("High on-target search-and-replace");
    }

    [Fact]
    public void AnalyzePrimeEditing_ProximalNick_ReturnsDoubleStrandBreakSurge()
    {
        var telemetry = new PrimeEditingTelemetry(
            EditTargetLocusId: "CFTR-DELTA-F508",
            PrimerBindingSiteLengthNucleotides: 14,
            ReverseTranscriptionTemplateLengthNucleotides: 15,
            PbsMeltingTemperatureCelsius: 38.0,
            RttGcContentPercent: 48.0,
            IsPe3NonEditedStrandNickApplied: true,
            NonEditedStrandNickDistanceBasePairs: 18.0, // Too close < 35 bp -> DSB!
            SecondaryStructureFreeEnergyDeltaG: -1.5,
            TargetGenomicLocusAccessibilityScore: 0.85
        );

        var result = _engine.AnalyzePrimeEditing(telemetry);

        result.PrimeEditingRegime.Should().Be("DOUBLE_STRAND_BREAK_INDEL_SURGE");
        result.IsEditingPrecisionHigh.Should().BeFalse();
        result.PredictedIndelFormationRatePercent.Should().BeGreaterThan(5.0);
        result.PegRnaOptimizationGuidance.Should().ContainEquivalentOf("Relocate sgRNA nick site");
    }

    [Fact]
    public void AnalyzePrimeEditing_HairpinStall_ReturnsHairpinStallRegime()
    {
        var telemetry = new PrimeEditingTelemetry(
            EditTargetLocusId: "HEXA-TAY-SACHS",
            PrimerBindingSiteLengthNucleotides: 14,
            ReverseTranscriptionTemplateLengthNucleotides: 22,
            PbsMeltingTemperatureCelsius: 37.0,
            RttGcContentPercent: 52.0,
            IsPe3NonEditedStrandNickApplied: false,
            NonEditedStrandNickDistanceBasePairs: 0.0,
            SecondaryStructureFreeEnergyDeltaG: -8.4, // Severe hairpin < -6.5
            TargetGenomicLocusAccessibilityScore: 0.80
        );

        var result = _engine.AnalyzePrimeEditing(telemetry);

        result.PrimeEditingRegime.Should().Be("PEGRNA_HAIRPIN_REVERSE_TRANSCRIPTION_STALL");
        result.IsEditingPrecisionHigh.Should().BeFalse();
        result.PegRnaOptimizationGuidance.Should().ContainEquivalentOf("synonymous nucleotide substitutions");
    }

    [Fact]
    public void AnalyzePrimeEditing_LowPbsTm_ReturnsPbsInstabilityWarning()
    {
        var telemetry = new PrimeEditingTelemetry(
            EditTargetLocusId: "DMD-EXON-51",
            PrimerBindingSiteLengthNucleotides: 9, // Length < 11
            ReverseTranscriptionTemplateLengthNucleotides: 14,
            PbsMeltingTemperatureCelsius: 24.5, // Low Tm < 28 C
            RttGcContentPercent: 45.0,
            IsPe3NonEditedStrandNickApplied: false,
            NonEditedStrandNickDistanceBasePairs: 0.0,
            SecondaryStructureFreeEnergyDeltaG: -1.0,
            TargetGenomicLocusAccessibilityScore: 0.75
        );

        var result = _engine.AnalyzePrimeEditing(telemetry);

        result.PrimeEditingRegime.Should().Be("PBS_HYBRIDIZATION_INSTABILITY_WARNING");
        result.IsEditingPrecisionHigh.Should().BeFalse();
        result.PegRnaOptimizationGuidance.Should().ContainEquivalentOf("Extend PBS by 2-3 nucleotides");
    }

    [Fact]
    public void AnalyzePrimeEditing_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzePrimeEditing(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
