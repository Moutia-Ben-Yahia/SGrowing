using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Marketing.Retention;

/// <summary>
/// Predictive customer churn risk classification tiers based on Cox survival hazard rate.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChurnRiskTier
{
    Low,
    Moderate,
    High,
    Critical
}

/// <summary>
/// Multi-armed bandit incentive strategies tailored for win-back and retention in Algeria.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncentiveType
{
    ExpressShippingVoucher,
    BundleDiscount15,
    VipConciergeCall,
    FreeGiftWithPurchase
}

/// <summary>
/// Omnichannel delivery channels available for proactive retention interventions.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetentionChannel
{
    WhatsAppInteractive,
    AutomatedVoiceConcierge,
    TargetedEmail
}

/// <summary>
/// Lifecycle status of a dispatched retention intervention.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterventionStatus
{
    Dispatched,
    Delivered,
    Engaged,
    Converted,
    Ignored
}

/// <summary>
/// Multidimensional behavioral signals used for predictive churn radar analysis.
/// </summary>
public record CustomerBehaviorSignals(
    string CustomerId,
    int DaysSinceLastOrder,
    int HistoricalOrderCadenceDays,
    double CadenceDecayRatio,
    decimal AvgOrderValue,
    double SupportTicketSentimentScore,
    int UnresolvedTicketCount,
    double WhatsAppMessageOpenRate,
    double EmailClickRate,
    double BrowsingDropOffRatio);

/// <summary>
/// Output of Cox Proportional Hazards survival analysis simulation.
/// </summary>
public record ChurnRiskAssessment(
    string CustomerId,
    double ChurnRiskScore,
    ChurnRiskTier RiskTier,
    double HazardRate,
    int ExpectedDaysToChurn,
    IReadOnlyList<string> PrimaryRiskFactors,
    DateTime AssessedAtUtc);

/// <summary>
/// A commercial retention incentive configured for multi-armed bandit allocation.
/// </summary>
public record RetentionIncentive(
    Guid IncentiveId,
    IncentiveType Type,
    string Description,
    decimal DiscountPercentage,
    decimal CostDzd,
    int ExpiryDays);

/// <summary>
/// Bandit allocation decision with exploration/exploitation metrics and optimal channel.
/// </summary>
public record BanditRecommendation(
    string CustomerId,
    RetentionIncentive SelectedIncentive,
    double ExplorationProbability,
    double EstimatedConversionLift,
    RetentionChannel RecommendedChannel);

/// <summary>
/// Auditable record of a dispatched omnichannel retention intervention.
/// </summary>
public record RetentionInterventionRecord(
    Guid InterventionId,
    string CustomerId,
    RetentionChannel ChannelUsed,
    RetentionIncentive? IncentiveApplied,
    InterventionStatus DispatchStatus,
    decimal RevenueSavedDzd,
    bool IsHoldoutControl,
    DateTime DispatchedAtUtc);

/// <summary>
/// Real-time multi-armed bandit arm performance statistics and conversion metrics.
/// </summary>
public record BanditArmStats(
    IncentiveType IncentiveType,
    int TotalPulls,
    int TotalConversions,
    double ConversionRate,
    decimal CumulativeRevenueLift);

/// <summary>
/// Aggregate metrics for randomized holdout experiments and incremental causal lift.
/// </summary>
public record RetentionExperimentMetrics(
    int TotalInterventions,
    int TreatmentCount,
    int HoldoutControlCount,
    int TreatmentConversions,
    int ControlConversions,
    double TreatmentConversionRate,
    double ControlConversionRate,
    double IncrementalCausalLift,
    decimal TotalRevenueSavedDzd,
    decimal TotalIncentiveCostDzd,
    decimal NetRevenueSavedDzd);

// API Contracts

public record AssessCustomerRiskRequest(CustomerBehaviorSignals Signals);

public record AssessCustomerRiskResponse(
    bool Success,
    ChurnRiskAssessment? Assessment,
    string? Message = null);

public record BatchRadarScanRequest(
    List<CustomerBehaviorSignals> Customers,
    double? RiskThreshold = null);

public record BatchRadarScanResponse(
    bool Success,
    int TotalScanned,
    int AtRiskCount,
    List<ChurnRiskAssessment> AtRiskCustomers,
    string? Message = null);

public record SelectIncentiveRequest(
    CustomerBehaviorSignals Signals,
    ChurnRiskAssessment? Assessment = null);

public record SelectIncentiveResponse(
    bool Success,
    BanditRecommendation? Recommendation,
    string? Message = null);

public record DispatchInterventionRequest(
    CustomerBehaviorSignals Signals,
    RetentionIncentive? ForcedIncentive = null,
    bool ForceHoldout = false,
    bool BypassHoldout = false);

public record DispatchInterventionResponse(
    bool Success,
    RetentionInterventionRecord? Intervention,
    string? Message = null);

public record RecordBanditFeedbackRequest(
    Guid? InterventionId,
    string? CustomerId,
    IncentiveType IncentiveType,
    bool Converted,
    decimal RevenueSavedDzd);

public record RecordBanditFeedbackResponse(
    bool Success,
    BanditArmStats UpdatedStats,
    string? Message = null);

public record BanditStatsResponse(
    bool Success,
    List<BanditArmStats> Arms,
    IncentiveType LeadingArm,
    double OverallConversionRate,
    decimal TotalRevenueSavedDzd);

public record RetentionEngineHealth(
    string Status,
    string Version,
    DateTime TimestampUtc,
    IReadOnlyDictionary<string, string> Capabilities);
