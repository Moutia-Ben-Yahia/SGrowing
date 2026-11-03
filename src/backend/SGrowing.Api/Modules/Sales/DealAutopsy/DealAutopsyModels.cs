namespace SGrowing.Api.Modules.Sales.DealAutopsy;

public enum LossReasonCategory
{
    CompetitorDisplacement,
    PricingAndTcoMisalignment,
    SecurityAndComplianceBlocker,
    ChampionDepartureOrReorg,
    FeatureGapOrRfpMismatch,
    BudgetFreezeOrNoDecision
}

public sealed record LossFactorSignal(
    string SignalId,
    string FactorName,
    LossReasonCategory Category,
    double InfluenceWeight,
    string KeyEvidenceExcerpt
);

public sealed record DealAutopsyRecord(
    string AutopsyId,
    string OpportunityId,
    string AccountName,
    double DealValueUsd,
    string SalesRepName,
    LossReasonCategory PrimaryLossCategory,
    double AutopsyConfidenceScore,
    IReadOnlyList<LossFactorSignal> DetectedSignals,
    DateTime ClosedLostAtUtc
);

public sealed record WinRateRecoveryPlaybook(
    string PlaybookId,
    string TargetOpportunityId,
    string RecommendedRecoveryStrategy,
    int ReEngagementCooldownDays,
    string ProposedValueAngle,
    double ProbabilityOfPipelineRevival
);

public sealed record DealAutopsyOverview(
    int TotalAutopsiesCompletedQuarter,
    double TotalLostPipelineValueUsd,
    double WinRateRecoveryPotentialPct,
    IReadOnlyList<DealAutopsyRecord> RecentAutopsies,
    IReadOnlyList<WinRateRecoveryPlaybook> ActivePlaybooks
);
