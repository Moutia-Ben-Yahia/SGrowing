using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Sales.TerritoryRebalancing;

public record AccountPotentialRecord(
    string AccountId,
    string AccountName,
    string Industry,
    decimal AnnualRevenue,
    int IcpFitScore,
    int ExpansionPotentialScore,
    int CompositePotentialScore,
    string CurrentRepId,
    string CurrentRepName,
    string AssignedTerritory
);

public record TerritoryWorkloadMetrics(
    string TerritoryId,
    string TerritoryName,
    string AssignedRepId,
    string RepName,
    int AccountCount,
    decimal TotalPipelinePotential,
    double WorkloadIndex,
    double QuotaAttainmentPct
);

public record RebalanceRecommendation(
    string AccountId,
    string AccountName,
    string FromRepId,
    string FromRepName,
    string ToRepId,
    string ToRepName,
    string Rationale,
    double ExpectedWorkloadDeltaPct
);

public record TerritoryRebalancingOverview(
    int TotalAccounts,
    int TotalReps,
    double GiniWorkloadIndex,
    double ProjectedEfficiencyGainPct,
    IReadOnlyList<TerritoryWorkloadMetrics> Territories,
    IReadOnlyList<RebalanceRecommendation> Recommendations
);

public record SimulateRebalanceCommand(
    string Strategy, // "Equilibrium", "GeoProximity", "HighValueSpecialization"
    int TargetMaxGiniIndex
);

public record SimulateRebalanceResult(
    string SimulationId,
    string Strategy,
    double OriginalGiniIndex,
    double SimulatedGiniIndex,
    int AccountsReallocatedCount,
    double ExpectedQuotaImprovementPct,
    IReadOnlyList<RebalanceRecommendation> PlannedMoves
);

public record ApplyRebalanceCommand(
    string SimulationId,
    int GracePeriodDays,
    string ApprovedBy
);

public record ApplyRebalanceResult(
    string RebalancePlanId,
    string Status, // "Scheduled", "Applied"
    int AccountsShifted,
    DateTime EffectiveDate,
    string AuditReceipt
);
