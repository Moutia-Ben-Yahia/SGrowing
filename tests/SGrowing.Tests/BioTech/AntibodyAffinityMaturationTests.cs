namespace SGrowing.Tests.BioTech;

using SGrowing.Api.Modules.BioTech.AntibodyAffinityMaturation;
using Xunit;

public class AntibodyAffinityMaturationTests
{
    private readonly AntibodyAffinityMaturationEngine _engine = new();

    [Fact]
    public void EvaluateCandidates_HighAffinityStableMutant_IsSelectedAsOptimalLead()
    {
        var request = new AntibodyMaturationRequest(
            AntibodyId: "MAB-HER2-OPTIMAL",
            TargetAntigenName: "HER2-Extracellular-Domain",
            WildTypeKdNanoMolar: 10.0,
            WildTypeMeltingTempCelsius: 75.0,
            CandidateMutations:
            [
                new CandidateMutation("H:Y98F", "CDR-H3", -1.2, 0.2, 0.22, false),
                new CandidateMutation("H:R102S", "CDR-H3", -0.4, 0.1, 0.20, false),
                new CandidateMutation("L:N32D", "CDR-L1", 0.5, 0.0, 0.18, false)
            ]);

        var result = _engine.EvaluateCandidates(request);

        Assert.Equal("MAB-HER2-OPTIMAL", result.AntibodyId);
        Assert.Equal("H:Y98F", result.OptimalLeadMutationCode);
        Assert.Equal(1, result.ViableCandidatesCount);

        var top = result.RankedMutants.First();
        Assert.Equal("H:Y98F", top.MutationCode);
        Assert.True(top.IsRecommended);
        Assert.True(top.FoldAffinityImprovement > 5.0); // exp(1.2 / 0.592) ~ 7.59x
        Assert.True(top.PredictedKdNanoMolar < 2.0);
    }

    [Fact]
    public void EvaluateCandidates_LiabilityMotifMutant_IsRejectedDespiteHighAffinity()
    {
        var request = new AntibodyMaturationRequest(
            AntibodyId: "MAB-PD1-LIABILITY",
            TargetAntigenName: "PD-1",
            WildTypeKdNanoMolar: 8.0,
            WildTypeMeltingTempCelsius: 72.0,
            CandidateMutations:
            [
                new CandidateMutation("H:W33C", "CDR-H1", -1.8, 0.3, 0.25, true) // Contains unpaired Cys liability
            ]);

        var result = _engine.EvaluateCandidates(request);

        Assert.Equal(0, result.ViableCandidatesCount);
        Assert.Equal("NONE_RECOMMENDED", result.OptimalLeadMutationCode);
        Assert.False(result.RankedMutants[0].IsRecommended);
        Assert.Contains("REJECTED: Contains sequence liability motif", result.RankedMutants[0].FitnessSummary);
    }

    [Fact]
    public void EvaluateCandidates_FoldDestabilizedMutant_IsFilteredOut()
    {
        var request = new AntibodyMaturationRequest(
            AntibodyId: "MAB-EGFR-UNSTABLE",
            TargetAntigenName: "EGFR",
            WildTypeKdNanoMolar: 5.0,
            WildTypeMeltingTempCelsius: 70.0,
            CandidateMutations:
            [
                new CandidateMutation("H:F100W", "CDR-H3", -1.5, 2.8, 0.28, false) // ddG_fold = 2.8 > 1.5 -> severe destabilization
            ]);

        var result = _engine.EvaluateCandidates(request);

        Assert.Equal(0, result.ViableCandidatesCount);
        Assert.False(result.RankedMutants[0].IsRecommended);
        Assert.Contains("REJECTED: Severe folding destabilization", result.RankedMutants[0].FitnessSummary);
    }

    [Fact]
    public void EvaluateCandidates_ZeroOrNegativeWildTypeParameters_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new AntibodyMaturationRequest(
            AntibodyId: "MAB-ERR",
            TargetAntigenName: "TargetX",
            WildTypeKdNanoMolar: -5.0,
            WildTypeMeltingTempCelsius: 70.0,
            CandidateMutations: []);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.EvaluateCandidates(invalid));
    }
}
