using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Retention.CustomerHealth;

/// <summary>
/// Health tier categorization for customer accounts based on composite health score.
/// Healthy: >= 75, AtRisk: 50-74, Critical: < 50.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HealthTier
{
    Healthy,
    AtRisk,
    Critical
}

/// <summary>
/// Early-warning churn risk trigger categories.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskTriggerType
{
    NpsDrop,
    TicketSpike,
    InactiveSeatRatio,
    ContractRenewalRisk,
    PaymentOverdue
}

/// <summary>
/// Risk severity levels for early warning indicators.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Types of automated CSM playbooks.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybookType
{
    ExecutiveSponsorCheckin,
    AdoptionWorkshop,
    EmergencyDiscountReview,
    TechnicalSwatTriage
}

/// <summary>
/// Execution priority of CSM playbooks.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybookPriority
{
    Low,
    Medium,
    High,
    Urgent
}

/// <summary>
/// Lifecycle status of a CSM playbook.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybookStatus
{
    Suggested,
    Active,
    Completed,
    Dismissed
}

/// <summary>
/// Early warning risk trigger record.
/// </summary>
public record EarlyWarningRiskTrigger(
    Guid TriggerId,
    RiskTriggerType TriggerType,
    RiskSeverity Severity,
    string Description,
    double CurrentMetricValue,
    double Threshold,
    DateTime DetectedAtUtc
);

/// <summary>
/// CSM Playbook actionable task recommendation.
/// </summary>
public record CsmPlaybookRecord(
    Guid PlaybookId,
    Guid AccountId,
    string AccountName,
    string Title,
    PlaybookType PlaybookType,
    PlaybookPriority Priority,
    PlaybookStatus Status,
    string Objective,
    string AssignedRole,
    List<string> RecommendedActions,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc
);

/// <summary>
/// Historical point-in-time snapshot of account health score.
/// </summary>
public record HealthScoreSnapshotRecord(
    Guid SnapshotId,
    DateTime RecordedAtUtc,
    double CompositeHealthScore,
    double AdoptionScore,
    double SentimentScore,
    double UtilizationScore,
    double CommercialScore,
    HealthTier HealthTier
);

/// <summary>
/// Core customer account health and retention radar record.
/// </summary>
public record CustomerAccountHealthRecord(
    Guid AccountId,
    Guid TenantId,
    string AccountName,
    string Domain,
    string Industry,
    string AssignedCsm,
    decimal AnnualRecurringRevenue,
    int TotalSeats,
    int ActiveSeats,
    double InactiveSeatRatio,
    int NpsScore,
    int PreviousNpsScore,
    int OpenTicketsCount,
    int UrgentTicketsCount,
    int DaysUntilRenewal,
    int DaysOverdueInvoice,
    double AdoptionScore,
    double SentimentScore,
    double UtilizationScore,
    double CommercialScore,
    double CompositeHealthScore,
    HealthTier HealthTier,
    double PredictedChurnProbability,
    DateTime LastEvaluatedAtUtc,
    List<EarlyWarningRiskTrigger> RiskTriggers,
    List<CsmPlaybookRecord> ActivePlaybooks,
    List<HealthScoreSnapshotRecord> HistoricalSnapshots,
    List<string> Tags
);

/// <summary>
/// Executive overview report for retention health radar.
/// </summary>
public record CustomerHealthRadarOverview(
    Guid TenantId,
    int TotalAccounts,
    int HealthyCount,
    int AtRiskCount,
    int CriticalCount,
    double AverageHealthScore,
    decimal TotalArr,
    decimal ArrAtRisk,
    int ActivePlaybookCount,
    List<CustomerAccountHealthRecord> Accounts
);

/// <summary>
/// Request payload to create a new customer account for health tracking.
/// </summary>
public record CreateCustomerAccountRequest(
    Guid TenantId,
    string AccountName,
    string Domain,
    string Industry,
    string AssignedCsm,
    decimal AnnualRecurringRevenue,
    int TotalSeats,
    int ActiveSeats,
    int NpsScore,
    int PreviousNpsScore,
    int OpenTicketsCount,
    int UrgentTicketsCount,
    int DaysUntilRenewal,
    int DaysOverdueInvoice,
    double AdoptionScore,
    double SentimentScore,
    double UtilizationScore,
    double CommercialScore,
    List<string>? Tags = null
);

/// <summary>
/// Request payload to update account metrics and recalculate health score.
/// </summary>
public record UpdateAccountMetricsRequest(
    int? TotalSeats = null,
    int? ActiveSeats = null,
    int? NpsScore = null,
    int? PreviousNpsScore = null,
    int? OpenTicketsCount = null,
    int? UrgentTicketsCount = null,
    int? DaysUntilRenewal = null,
    int? DaysOverdueInvoice = null,
    double? AdoptionScore = null,
    double? SentimentScore = null,
    double? UtilizationScore = null,
    double? CommercialScore = null,
    string? AssignedCsm = null
);

/// <summary>
/// Request payload to add a custom or proactive playbook.
/// </summary>
public record CreatePlaybookRequest(
    string Title,
    PlaybookType PlaybookType,
    PlaybookPriority Priority,
    string Objective,
    string AssignedRole,
    List<string> RecommendedActions,
    string? Notes = null
);

/// <summary>
/// Request payload to change playbook status.
/// </summary>
public record UpdatePlaybookStatusRequest(
    PlaybookStatus Status,
    string? Notes = null
);
