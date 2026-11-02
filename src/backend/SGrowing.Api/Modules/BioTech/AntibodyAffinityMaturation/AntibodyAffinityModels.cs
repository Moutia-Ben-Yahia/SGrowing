namespace SGrowing.Api.Modules.BioTech.AntibodyAffinityMaturation;

public record CandidateMutation(
    string MutationCode,
    string CdrRegion,
    double DeltaDeltaGBindingKcalMol,
    double DeltaDeltaGFoldingKcalMol,
    double SpatialAggregationPropensity,
    bool ContainsLiabilityMotif);

public record AntibodyMaturationRequest(
    string AntibodyId,
    string TargetAntigenName,
    double WildTypeKdNanoMolar,
    double WildTypeMeltingTempCelsius,
    List<CandidateMutation> CandidateMutations);

public record MaturedMutantScore(
    string MutationCode,
    string CdrRegion,
    double PredictedKdNanoMolar,
    double PredictedMeltingTempCelsius,
    double FoldAffinityImprovement,
    double ParetoScore,
    bool IsRecommended,
    string FitnessSummary);

public record AntibodyMaturationResult(
    string AntibodyId,
    string TargetAntigenName,
    int TotalCandidatesEvaluated,
    int ViableCandidatesCount,
    List<MaturedMutantScore> RankedMutants,
    string OptimalLeadMutationCode,
    string GlobalMaturationSummary);
