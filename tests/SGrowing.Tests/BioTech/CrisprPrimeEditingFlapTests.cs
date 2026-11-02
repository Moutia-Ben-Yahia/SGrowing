namespace SGrowing.Tests.BioTech;

using SGrowing.Api.Modules.BioTech.CrisprPrimeEditingFlap;
using Xunit;

public class CrisprPrimeEditingFlapTests
{
    private readonly PrimeEditingFlapEngine _engine = new();

    [Fact]
    public void AnalyzeDesign_OptimalPe5Configuration_ApprovesHighEfficiencyLead()
    {
        // Tm = 32 C, ddG = -14.0 - (-9.5) = -4.5 kcal/mol
        // PE3 distance = 60 bp, MMR evasion = 0.90
        var telemetry = new PrimeEditingDesignTelemetry(
            DesignId: "PE5-HEK3-OPT",
            TargetGeneName: "HEK3",
            PegRnaPbsLengthNt: 13,
            PegRnaRttLengthNt: 16,
            PbsMeltingTempCelsius: 32.0,
            EditedFlapFreeEnergyKcalMol: -14.0,
            UneditedFlapFreeEnergyKcalMol: -9.5,
            Pe3SecondaryNickDistanceBp: 60,
            MmrEvasionScore: 0.90);

        var result = _engine.AnalyzeDesign(telemetry);

        Assert.Equal("PE5-HEK3-OPT", result.DesignId);
        Assert.Equal("HEK3", result.TargetGeneName);
        Assert.True(result.IsDesignApproved);
        Assert.True(result.PredictedPrimeEditingEfficiencyPercent > 50.0);
        Assert.True(result.PredictedIndelByproductPercent <= 1.5);
        Assert.Equal("OPTIMAL_PE5_TRANSDUCTION", result.FlapResolutionState);
        Assert.Contains("HIGH-PRECISION EDITING", result.MolecularMechanisticSummary);
    }

    [Fact]
    public void AnalyzeDesign_TooCloseSecondaryNick_FlagsExcessiveIndelHazard()
    {
        // PE3 distance = 18 bp (< 35 bp) -> causes double-strand break and indel jump
        var telemetry = new PrimeEditingDesignTelemetry(
            DesignId: "PE3-CLOSE-NICK",
            TargetGeneName: "HBB-SickleCell",
            PegRnaPbsLengthNt: 13,
            PegRnaRttLengthNt: 15,
            PbsMeltingTempCelsius: 32.0,
            EditedFlapFreeEnergyKcalMol: -12.0,
            UneditedFlapFreeEnergyKcalMol: -10.0,
            Pe3SecondaryNickDistanceBp: 18,
            MmrEvasionScore: 0.85);

        var result = _engine.AnalyzeDesign(telemetry);

        Assert.False(result.IsDesignApproved);
        Assert.True(result.PredictedIndelByproductPercent > 3.0);
        Assert.Equal("EXCESSIVE_INDEL_HAZARD", result.FlapResolutionState);
        Assert.Contains("Reposition PE3 secondary nick", result.OptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeDesign_PoorMmrEvasion_FlagsMmrReversionRisk()
    {
        // MMR evasion = 0.20 (< 0.45)
        var telemetry = new PrimeEditingDesignTelemetry(
            DesignId: "PE3-LOW-MMR",
            TargetGeneName: "FANCF",
            PegRnaPbsLengthNt: 13,
            PegRnaRttLengthNt: 15,
            PbsMeltingTempCelsius: 32.0,
            EditedFlapFreeEnergyKcalMol: -12.0,
            UneditedFlapFreeEnergyKcalMol: -10.0,
            Pe3SecondaryNickDistanceBp: 65,
            MmrEvasionScore: 0.20);

        var result = _engine.AnalyzeDesign(telemetry);

        Assert.Equal("SUBOPTIMAL_MMR_REVERSION_RISK", result.FlapResolutionState);
        Assert.Contains("Incorporate silent third-codon position", result.OptimizationRecommendation);
    }

    [Fact]
    public void AnalyzeDesign_NegativePbsLength_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new PrimeEditingDesignTelemetry(
            DesignId: "ERR-001",
            TargetGeneName: "ERR",
            PegRnaPbsLengthNt: -5,
            PegRnaRttLengthNt: 10,
            PbsMeltingTempCelsius: 30.0,
            EditedFlapFreeEnergyKcalMol: -10.0,
            UneditedFlapFreeEnergyKcalMol: -10.0,
            Pe3SecondaryNickDistanceBp: 50,
            MmrEvasionScore: 0.8);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeDesign(invalid));
    }
}
