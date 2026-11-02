using SGrowing.Api.Modules.BioTech.OrganoidLightSheetMorphogenesis;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class OrganoidMorphogenesisTests
{
    private readonly OrganoidMorphogenesisEngine _engine = new();

    [Fact]
    public void AnalyzeMorphogenesis_NominalOrganoid_ReturnsBatchCertified()
    {
        var telemetry = new OrganoidMorphogenesisTelemetry
        {
            OrganoidCultureBatchId = "ORG-BRAIN-BATCH-01",
            OrganoidLineage = "HumanCerebralCortical_BrainOrganoid",
            CultureDayPostDifferentiation = 45,
            OrganoidRadiusMicrons = 420.0,
            NecroticCoreRadiusMicrons = 48.0,
            SphericityIndex = 0.89,
            FractalBranchingDimension = 1.62,
            TerminalBudMorphogenesisCount = 18,
            DeepTissueCellViabilityPercent = 92.4,
            LightSheetPhotobleachingApoptosisPercent = 1.8
        };

        var result = _engine.AnalyzeMorphogenesis(telemetry);

        Assert.NotNull(result);
        Assert.Equal("ORG-BRAIN-BATCH-01", result.OrganoidCultureBatchId);
        Assert.True(result.IsNecroticCoreHypoxiaBounded);
        Assert.True(result.IsBranchingMorphogenesisActive);
        Assert.True(result.IsPhototoxicityControlled);
        Assert.True(result.IsOrganoidBatchQualityCertified);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.NecroticCoreFractionRatio < 0.15);
        Assert.True(result.MorphologicalIntegrityScore >= 8.0);
    }

    [Fact]
    public void AnalyzeMorphogenesis_ExpandedNecroticCore_TriggersHypoxiaAlert()
    {
        var telemetry = new OrganoidMorphogenesisTelemetry
        {
            OrganoidCultureBatchId = "ORG-BRAIN-BATCH-02",
            OrganoidRadiusMicrons = 450.0,
            NecroticCoreRadiusMicrons = 135.0 // Ratio = 0.30 > 0.20
        };

        var result = _engine.AnalyzeMorphogenesis(telemetry);

        Assert.False(result.IsNecroticCoreHypoxiaBounded);
        Assert.False(result.IsOrganoidBatchQualityCertified);
        Assert.Contains("ORGANOID_HYPOXIC_NECROTIC_CORE_EXPANSION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeMorphogenesis_ArrestedBranching_TriggersMorphogenesisArrestAlert()
    {
        var telemetry = new OrganoidMorphogenesisTelemetry
        {
            OrganoidCultureBatchId = "ORG-BRAIN-BATCH-03",
            FractalBranchingDimension = 1.15,
            TerminalBudMorphogenesisCount = 4
        };

        var result = _engine.AnalyzeMorphogenesis(telemetry);

        Assert.False(result.IsBranchingMorphogenesisActive);
        Assert.False(result.IsOrganoidBatchQualityCertified);
        Assert.Contains("BRANCHING_MORPHOGENESIS_ARREST", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeMorphogenesis_ExcessiveLaserPhototoxicity_TriggersBleachingAlert()
    {
        var telemetry = new OrganoidMorphogenesisTelemetry
        {
            OrganoidCultureBatchId = "ORG-BRAIN-BATCH-04",
            LightSheetPhotobleachingApoptosisPercent = 5.2
        };

        var result = _engine.AnalyzeMorphogenesis(telemetry);

        Assert.False(result.IsPhototoxicityControlled);
        Assert.False(result.IsOrganoidBatchQualityCertified);
        Assert.Contains("LIGHT_SHEET_PHOTOTOXICITY_BLEACHING_SURGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeMorphogenesis_LowDeepTissueViability_FailsCertification()
    {
        var telemetry = new OrganoidMorphogenesisTelemetry
        {
            OrganoidCultureBatchId = "ORG-BRAIN-BATCH-05",
            DeepTissueCellViabilityPercent = 78.0
        };

        var result = _engine.AnalyzeMorphogenesis(telemetry);

        Assert.False(result.IsOrganoidBatchQualityCertified);
    }
}
