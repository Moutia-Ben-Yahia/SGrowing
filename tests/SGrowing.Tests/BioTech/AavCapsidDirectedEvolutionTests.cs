using System;
using SGrowing.Api.Modules.BioTech.AavCapsidDirectedEvolution;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class AavCapsidDirectedEvolutionTests
{
    private readonly AavCapsidDirectedEvolutionEngine _engine = new();

    [Fact]
    public void AnalyzeCapsidEvolution_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeCapsidEvolution(null!));
    }

    [Fact]
    public void AnalyzeCapsidEvolution_EmptyVariantId_ThrowsArgumentException()
    {
        var telemetry = new AavCapsidTelemetry(
            CapsidVariantId: "",
            ParentalSerotype: "AAV9",
            InsertionVariableRegion: "VR-VIII",
            PeptideInsertSequence: "TLAVPFK",
            TargetTissueVectorGenomesPerCell: 15.0,
            LiverBiodistributionVectorGenomesPerCell: 1.0,
            NeutralizingAntibodyEvasionTiterRatio: 3.0,
            PackagingYieldViralGenomesPerMl: 1.2e13,
            TranscytosisEndothelialEfficiencyPercent: 80.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeCapsidEvolution(telemetry));
    }

    [Fact]
    public void AnalyzeCapsidEvolution_HighSelectivityVariant_ReturnsOptimalNeurotropicRegime()
    {
        var telemetry = new AavCapsidTelemetry(
            CapsidVariantId: "AAV-PHP-eB2",
            ParentalSerotype: "AAV9",
            InsertionVariableRegion: "VR-VIII",
            PeptideInsertSequence: "AQTLAVPFKAQ",
            TargetTissueVectorGenomesPerCell: 22.5,
            LiverBiodistributionVectorGenomesPerCell: 0.9,
            NeutralizingAntibodyEvasionTiterRatio: 3.5,
            PackagingYieldViralGenomesPerMl: 1.8e13,
            TranscytosisEndothelialEfficiencyPercent: 85.0
        );

        var result = _engine.AnalyzeCapsidEvolution(telemetry);

        Assert.NotNull(result);
        Assert.Equal("AAV-PHP-eB2", result.CapsidVariantId);
        Assert.True(result.HepaticDeTargetingRatio >= 15.0);
        Assert.True(result.IsVariantClinicallyViable);
        Assert.Equal("OPTIMAL_NEUROTROPIC_HEPATIC_DETARGETED_REGIME", result.CapsidEvolutionRegime);
    }

    [Fact]
    public void AnalyzeCapsidEvolution_SeverelyLowPackagingYield_ReturnsPackagingFailure()
    {
        var telemetry = new AavCapsidTelemetry(
            CapsidVariantId: "AAV-MUT-UNSTABLE",
            ParentalSerotype: "AAV2",
            InsertionVariableRegion: "VR-IV",
            PeptideInsertSequence: "WRYWPPF",
            TargetTissueVectorGenomesPerCell: 5.0,
            LiverBiodistributionVectorGenomesPerCell: 0.2,
            NeutralizingAntibodyEvasionTiterRatio: 1.0,
            PackagingYieldViralGenomesPerMl: 2.5e11, // Less than 1e12!
            TranscytosisEndothelialEfficiencyPercent: 40.0
        );

        var result = _engine.AnalyzeCapsidEvolution(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CRITICAL_CAPSID_ASSEMBLY_PACKAGING_FAILURE", result.CapsidEvolutionRegime);
        Assert.False(result.IsVariantClinicallyViable);
        Assert.Contains("destabilization", result.TranslationalGuidance);
    }

    [Fact]
    public void AnalyzeCapsidEvolution_HighLiverSequestration_ReturnsHepatotoxicHazard()
    {
        var telemetry = new AavCapsidTelemetry(
            CapsidVariantId: "AAV-WT-CONTROL",
            ParentalSerotype: "AAV9",
            InsertionVariableRegion: "NONE",
            PeptideInsertSequence: "-",
            TargetTissueVectorGenomesPerCell: 2.1,
            LiverBiodistributionVectorGenomesPerCell: 48.0, // High liver tropism!
            NeutralizingAntibodyEvasionTiterRatio: 1.0,
            PackagingYieldViralGenomesPerMl: 3.0e13,
            TranscytosisEndothelialEfficiencyPercent: 12.0
        );

        var result = _engine.AnalyzeCapsidEvolution(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SEVERE_LIVER_SEQUESTRATION_HEPATOTOXIC_HAZARD", result.CapsidEvolutionRegime);
        Assert.False(result.IsVariantClinicallyViable);
    }
}
