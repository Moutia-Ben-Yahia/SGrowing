using SGrowing.Api.Modules.BioTech.HighMolecularWeightDnaExtraction;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class HmwDnaExtractionTests
{
    private readonly HmwDnaExtractionEngine _engine = new();

    [Fact]
    public void AnalyzeExtraction_OptimalIntactSample_ReturnsUltraLongReady()
    {
        var telemetry = new HmwDnaExtractionTelemetry
        {
            ExtractionBatchId = "HMW-BATCH-001",
            SampleType = "MammalianWholeBlood",
            DnaConcentrationNgPerUl = 92.0,
            AbsorbanceRatio260_280 = 1.88,
            AbsorbanceRatio260_230 = 2.18,
            FragmentLengthN50Kbp = 58.2,
            UltraLongFractionAbove100KbpPercent = 32.0,
            PipettingHydrodynamicShearRateSecInv = 95.0,
            ActiveNanoporePoreFractionPercent = 90.0,
            TranslocationSpeedBasesPerSecond = 420.0
        };

        var result = _engine.AnalyzeExtraction(telemetry);

        Assert.Equal("HMW-BATCH-001", result.ExtractionBatchId);
        Assert.True(result.IsLengthProfileUltraLongReady);
        Assert.True(result.IsChemicalPurityCompliant);
        Assert.True(result.IsHydrodynamicShearControlled);
        Assert.True(result.IsNanoporeTranslocationOptimal);
        Assert.True(result.CalculatedSequencingReadinessScorePercent >= 90.0);
        Assert.True(result.EstimatedPoreBlockingHazardIndex <= 3.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeExtraction_SevereShearFragmentation_TriggersShearAlert()
    {
        var telemetry = new HmwDnaExtractionTelemetry
        {
            ExtractionBatchId = "HMW-SHEARED",
            FragmentLengthN50Kbp = 18.5, // Below 35 kbp
            UltraLongFractionAbove100KbpPercent = 6.0,
            PipettingHydrodynamicShearRateSecInv = 450.0 // Excessive shear
        };

        var result = _engine.AnalyzeExtraction(telemetry);

        Assert.False(result.IsLengthProfileUltraLongReady);
        Assert.False(result.IsHydrodynamicShearControlled);
        Assert.Contains("HYDRODYNAMIC_SHEAR_FRAGMENTATION_ALERT", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeExtraction_ChaotropicSaltCarryover_TriggersPoreBlockingAlert()
    {
        var telemetry = new HmwDnaExtractionTelemetry
        {
            ExtractionBatchId = "HMW-SALT-POLLUTED",
            AbsorbanceRatio260_230 = 1.45, // Salt / phenol carryover
            ActiveNanoporePoreFractionPercent = 62.0
        };

        var result = _engine.AnalyzeExtraction(telemetry);

        Assert.False(result.IsChemicalPurityCompliant);
        Assert.False(result.IsNanoporeTranslocationOptimal);
        Assert.True(result.EstimatedPoreBlockingHazardIndex > 3.5);
        Assert.Contains("CHAOTROPIC_SALT_PHENOL_PORE_BLOCKING", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeExtraction_ProteinHistoneResidual_TriggersProteinAlert()
    {
        var telemetry = new HmwDnaExtractionTelemetry
        {
            ExtractionBatchId = "HMW-PROTEIN-CONTAMINATED",
            AbsorbanceRatio260_280 = 1.62 // Protein contamination
        };

        var result = _engine.AnalyzeExtraction(telemetry);

        Assert.False(result.IsChemicalPurityCompliant);
        Assert.Contains("PROTEIN_HISTONE_RESIDUAL_CONTAMINATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeExtraction_ReadinessScoreDegrades_WithCompoundedFailures()
    {
        var perfectTelemetry = new HmwDnaExtractionTelemetry
        {
            ExtractionBatchId = "HMW-PERFECT",
            FragmentLengthN50Kbp = 65.0,
            AbsorbanceRatio260_280 = 1.88,
            AbsorbanceRatio260_230 = 2.20
        };

        var degradedTelemetry = new HmwDnaExtractionTelemetry
        {
            ExtractionBatchId = "HMW-DEGRADED",
            FragmentLengthN50Kbp = 15.0,
            AbsorbanceRatio260_280 = 1.50,
            AbsorbanceRatio260_230 = 1.30,
            PipettingHydrodynamicShearRateSecInv = 600.0,
            ActiveNanoporePoreFractionPercent = 40.0
        };

        var perfectResult = _engine.AnalyzeExtraction(perfectTelemetry);
        var degradedResult = _engine.AnalyzeExtraction(degradedTelemetry);

        Assert.True(perfectResult.CalculatedSequencingReadinessScorePercent > degradedResult.CalculatedSequencingReadinessScorePercent);
    }
}
