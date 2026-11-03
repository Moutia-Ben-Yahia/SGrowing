namespace SGrowing.Api.Modules.Sales.QuotaCapacity;

public record TerritoryCapacityProfile(
    string TerritoryId,
    string TerritoryName,
    string Region,
    int AssignedRepsCount,
    decimal TotalAnnualQuotaEur,
    decimal QuotaPerRepEur,
    decimal TotalAddressableMarketEur,
    decimal ActivePipelineEur,
    decimal CurrentPipelineCoverageRatio,
    string CapacityStatus, // Balanced, OverCapacity, WhitespaceSurge
    decimal RepBurnoutRiskIndex,
    DateTime LastUpdatedUtc
);

public record ImbalanceAlert(
    string AlertId,
    string TerritoryId,
    string TerritoryName,
    string ImbalanceType, // QuotaBurnout, WhitespaceSurge
    string Severity,
    int RecommendedHeadcountDelta,
    decimal GapAmountEur,
    string RecommendedAction
);

public record RebalanceRequest(
    string TerritoryId,
    int AddHeadcount,
    decimal TargetCoverageRatio
);

public record RebalanceResult(
    string TerritoryId,
    int PreviousHeadcount,
    int NewHeadcount,
    decimal OptimizedQuotaPerRepEur,
    decimal ProjectedPipelineCoverageRatio,
    string NewCapacityStatus,
    string AuditLogSummary
);
