namespace SGrowing.Api.Modules.Infrastructure.FinOps;

/// <summary>
/// Cloud infrastructure provider.
/// </summary>
public enum CloudProvider
{
    Aws = 1,
    Azure = 2,
    Gcp = 3,
    LocalSovereignDc = 4
}

/// <summary>
/// Pricing commitment tier.
/// </summary>
public enum PricingTier
{
    OnDemand = 1,
    SpotPreemptible = 2,
    Reserved1Year = 3,
    Reserved3Year = 4
}

/// <summary>
/// Status of a spot instance preemption evacuation.
/// </summary>
public enum EvictionMigrationStatus
{
    NoticeReceived = 1,
    Draining = 2,
    EvacuatedSuccessfully = 3,
    TerminatedWithDrop = 4
}

/// <summary>
/// Recorded resource consumption metrics for a specific tenant workload.
/// </summary>
public sealed record TenantResourceUsage(
    string UsageId,
    string TenantId,
    string ServiceName,
    double CpuMillicoresHours,
    double MemoryGigabyteHours,
    double StorageGigabytes,
    double EgressGigabytes,
    DateTime RecordedAtUtc
);

/// <summary>
/// Granular cost allocation statement breaking down compute, RAM, storage, and network costs.
/// </summary>
public sealed record TenantCostAllocationStatement(
    string StatementId,
    string TenantId,
    string BillingPeriod,
    decimal ComputeCostDzd,
    decimal MemoryCostDzd,
    decimal StorageCostDzd,
    decimal NetworkEgressCostDzd,
    decimal TotalCostDzd,
    decimal AllocatedPriceWithMarginDzd,
    decimal GrossMarginDzd,
    DateTime GeneratedAtUtc
);

/// <summary>
/// Spot instance eviction preemption notification event.
/// </summary>
public sealed record SpotPreemptionAlert(
    string AlertId,
    string NodeId,
    CloudProvider Provider,
    string Region,
    DateTime NoticeReceivedAtUtc,
    DateTime TerminationDeadlineUtc,
    IReadOnlyList<string> WorkloadIds,
    EvictionMigrationStatus Status,
    DateTime? CompletedAtUtc = null
);

/// <summary>
/// Workload rightsizing recommendation based on P99 utilization profiles.
/// </summary>
public sealed record RightSizingRecommendation(
    string WorkloadId,
    string ServiceName,
    double CurrentCpuCores,
    double RecommendedCpuCores,
    double CurrentMemoryGb,
    double RecommendedMemoryGb,
    decimal CurrentMonthlyCostDzd,
    decimal OptimizedMonthlyCostDzd,
    decimal MonthlySavingsDzd,
    double SavingsPercentage,
    bool IsOverprovisioned
);

/// <summary>
/// Historical usage sample for rightsizing analysis.
/// </summary>
public sealed record WorkloadUsageMetrics(
    string WorkloadId,
    string ServiceName,
    double CurrentAllocatedCpuCores,
    double CurrentAllocatedMemoryGb,
    double P99CpuUtilizationPercent,
    double P99MemoryUtilizationPercent,
    decimal CurrentHourlyCostDzd
);

/// <summary>
/// Request to generate a tenant FinOps cost allocation statement.
/// </summary>
public sealed record GenerateCostStatementRequest(
    string TenantId,
    string BillingPeriod,
    IReadOnlyList<TenantResourceUsage> Usages,
    decimal MarginMultiplier = 1.30m // 30% gross margin target
);
