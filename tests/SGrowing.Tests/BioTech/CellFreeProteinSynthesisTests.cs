using System;
using SGrowing.Api.Modules.BioTech.CellFreeProteinSynthesisExpression;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class CellFreeProteinSynthesisTests
{
    private readonly CellFreeProteinSynthesisEngine _engine = new();

    [Fact]
    public void AnalyzeSynthesis_OptimalParameters_ReturnsHighYieldStable()
    {
        var telemetry = new CellFreeProteinSynthesisTelemetry
        {
            BatchId = "CFPS-BATCH-001",
            TargetProteinName = "Nanobody-GFP",
            LinearDnaConcentrationNm = 12.0,
            GamSInhibitorConcentrationUm = 4.0,
            RareCodonStallingIndex = 0.8,
            RbsHairpinFreeEnergyKcalPerMol = -7.5,
            MagnesiumConcentrationMm = 12.0,
            PotassiumConcentrationMm = 150.0,
            ReactionDurationHours = 8.0,
            UninhibitedDnaDegradationConstantPerMin = 0.05
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.Equal("CFPS-BATCH-001", result.BatchId);
        Assert.True(result.IsDnaSufficientlyProtected);
        Assert.True(result.IsRbsFreeOfStrongSecondaryStructures);
        Assert.True(result.IsYieldTargetAchieved);
        Assert.True(result.ProjectedProteinYieldMgPerMl >= 0.75);
        Assert.True(result.DnaDegradationHalfLifeMinutes >= 120.0);
        Assert.Equal("HIGH_YIELD_CELL_FREE_SYNTHESIS_STABLE", result.ExpressionRegime);
    }

    [Fact]
    public void AnalyzeSynthesis_LowGamSProtection_TriggersExonucleaseDigestion()
    {
        var telemetry = new CellFreeProteinSynthesisTelemetry
        {
            BatchId = "CFPS-DEGRADE-002",
            TargetProteinName = "Antibody-ScFv",
            LinearDnaConcentrationNm = 8.0,
            GamSInhibitorConcentrationUm = 0.1, // very low protection
            RareCodonStallingIndex = 1.0,
            RbsHairpinFreeEnergyKcalPerMol = -8.0,
            MagnesiumConcentrationMm = 12.0,
            PotassiumConcentrationMm = 150.0,
            UninhibitedDnaDegradationConstantPerMin = 0.06
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.False(result.IsDnaSufficientlyProtected);
        Assert.Equal("EXONUCLEASE_TEMPLATE_RAPID_DIGESTION", result.ExpressionRegime);
        Assert.Contains("Supplement GamS concentration", result.OptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeSynthesis_TightRbsHairpin_TriggersTranslationBlockade()
    {
        var telemetry = new CellFreeProteinSynthesisTelemetry
        {
            BatchId = "CFPS-HAIRPIN-003",
            TargetProteinName = "Enzyme-Luciferase",
            LinearDnaConcentrationNm = 10.0,
            GamSInhibitorConcentrationUm = 3.5,
            RareCodonStallingIndex = 1.0,
            RbsHairpinFreeEnergyKcalPerMol = -18.2, // severe hairpin <= -14 kcal/mol
            MagnesiumConcentrationMm = 12.0,
            PotassiumConcentrationMm = 150.0
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.False(result.IsRbsFreeOfStrongSecondaryStructures);
        Assert.Equal("RBS_SECONDARY_HAIRPIN_TRANSLATION_BLOCKADE", result.ExpressionRegime);
        Assert.Contains("forms a tight secondary hairpin", result.OptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeSynthesis_ExtremeCodonStalling_FlagsQueueCollapse()
    {
        var telemetry = new CellFreeProteinSynthesisTelemetry
        {
            BatchId = "CFPS-STALL-004",
            TargetProteinName = "Complex-MultiDomain",
            LinearDnaConcentrationNm = 10.0,
            GamSInhibitorConcentrationUm = 3.5,
            RareCodonStallingIndex = 4.8, // severe stalling >= 4.0
            RbsHairpinFreeEnergyKcalPerMol = -8.0,
            MagnesiumConcentrationMm = 12.0,
            PotassiumConcentrationMm = 150.0
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.Equal("RARE_CODON_RIBOSOME_QUEUE_COLLAPSE", result.ExpressionRegime);
        Assert.Contains("induces ribosome queuing", result.OptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeSynthesis_NullOrZeroConcentration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeSynthesis(null!));

        var invalidTelemetry = new CellFreeProteinSynthesisTelemetry
        {
            BatchId = "CFPS-INV",
            LinearDnaConcentrationNm = 0.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeSynthesis(invalidTelemetry));
    }
}
