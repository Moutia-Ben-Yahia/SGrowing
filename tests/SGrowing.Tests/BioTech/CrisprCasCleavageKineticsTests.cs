namespace SGrowing.Tests.BioTech;

using FluentAssertions;
using SGrowing.Api.Modules.BioTech.CrisprCasCleavageKinetics;
using Xunit;

public class CrisprCasCleavageKineticsTests
{
    private readonly CrisprCasCleavageKineticsEngine _engine = new();

    [Fact]
    public void EvaluateOffTargetCleavage_MultipleSeedMismatches_YieldsNegligibleCleavageRisk()
    {
        var guide = new CrisprGuideRna("G-HBB-01", "ACCGTTAGGCTAAACCGGTT", "HBB", "TTTA");

        // 3 seed mismatches at positions 1, 2, 3
        var loci = new List<OffTargetLocus>
        {
            new("LOCUS-01", "chr11", 5227000, "ATTTTTAGGCTAAACCGGTT", "TTTA", "Intergenic")
        };

        var request = new CrisprCleavageEvaluationRequest(guide, loci);
        var result = _engine.EvaluateOffTargetCleavage(request);

        result.GuideApprovedForTherapy.Should().BeTrue();
        result.HazardousOffTargetsCount.Should().Be(0);
        var pred = result.Predictions.First();
        pred.SeedMismatchCount.Should().Be(3);
        pred.CleavageProbabilityPercent.Should().BeLessThan(0.001);
        pred.ExceedsSafetyThreshold.Should().BeFalse();
    }

    [Fact]
    public void EvaluateOffTargetCleavage_NearPerfectExonicMatch_FlagsHazardousOffTarget()
    {
        var guide = new CrisprGuideRna("G-PCSK9", "GCTGATGGATCTTGTAGCAC", "PCSK9", "TTTC");

        // Perfect seed match with only 1 distal mismatch at position 18 in tumor suppressor gene
        var loci = new List<OffTargetLocus>
        {
            new("LOCUS-TP53", "chr17", 7675000, "GCTGATGGATCTTGTAGCAC", "TTTC", "Exonic_TP53")
        };

        var request = new CrisprCleavageEvaluationRequest(guide, loci);
        var result = _engine.EvaluateOffTargetCleavage(request);

        result.GuideApprovedForTherapy.Should().BeFalse();
        result.HazardousOffTargetsCount.Should().Be(1);
        result.ClinicalSafetyClassification.Should().Contain("UNSAFE_GENOME_WIDE_MUTAGENESIS");
        result.Predictions.First().ExceedsSafetyThreshold.Should().BeTrue();
    }

    [Fact]
    public void EvaluateOffTargetCleavage_InvalidPam_DramaticallyPenalizesCleavage()
    {
        var guide = new CrisprGuideRna("G-TEST", "AAAAAAAAAAAAAAAAAAAA", "GENE", "TTTA");

        // Identical 20nt protospacer, but PAM is mutated from TTTA to CCGA
        var loci = new List<OffTargetLocus>
        {
            new("LOCUS-BAD-PAM", "chr1", 100000, "AAAAAAAAAAAAAAAAAAAA", "CCGA", "Intronic")
        };

        var request = new CrisprCleavageEvaluationRequest(guide, loci);
        var result = _engine.EvaluateOffTargetCleavage(request);

        var pred = result.Predictions.First();
        pred.DeltaDeltaGKcalMol.Should().BeGreaterThan(3.5);
        pred.CleavageProbabilityPercent.Should().BeLessThan(0.5);
    }

    [Fact]
    public void EvaluateOffTargetCleavage_TherapeuticGuideApproved_WhenZeroHazardousLoci()
    {
        var guide = new CrisprGuideRna("G-SAFE", "CGATCGATCGATCGATCGAT", "B2M", "TTTA");

        var loci = new List<OffTargetLocus>
        {
            new("LOCUS-A", "chr2", 200000, "TTTTTTATCGATCGATCGAT", "TTTA", "Intergenic"),
            new("LOCUS-B", "chr5", 500000, "CGATCGATCGATCGATTTTT", "CCCA", "Intergenic")
        };

        var request = new CrisprCleavageEvaluationRequest(guide, loci);
        var result = _engine.EvaluateOffTargetCleavage(request);

        result.GuideApprovedForTherapy.Should().BeTrue();
        result.ClinicalSafetyClassification.Should().Contain("APPROVED_FOR_CELL_THERAPY");
    }
}
