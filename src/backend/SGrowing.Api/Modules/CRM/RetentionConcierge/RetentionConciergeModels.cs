namespace SGrowing.Api.Modules.CRM.RetentionConcierge;

/// <summary>
/// Customer loyalty tier classification.
/// </summary>
public enum CustomerTier
{
    Standard = 1,
    Silver = 2,
    Gold = 3,
    PlatinumVIP = 4
}

/// <summary>
/// Predictive churn hazard risk tier.
/// </summary>
public enum ChurnRiskLevel
{
    Minimal = 1,       // Hazard < 0.20
    Moderate = 2,      // 0.20 <= Hazard < 0.50
    High = 3,          // 0.50 <= Hazard < 0.75
    CriticalImminent = 4 // Hazard >= 0.75
}

/// <summary>
/// Type of proactive retention goodwill incentive.
/// </summary>
public enum DelightIncentiveType
{
    ExpeditedShippingWaiver = 1,
    BonusLoyaltyCredits = 2,
    PersonalizedDiscountCode = 3,
    ComplimentaryGiftBasket = 4,
    ExecutiveAccountManagerCall = 5,
    ExclusivePrivateSaleAccess = 6
}

/// <summary>
/// Status of an automated delight intervention.
/// </summary>
public enum DelightActionStatus
{
    Proposed = 1,
    Dispatched = 2,
    Redeemed = 3,
    Expired = 4
}

/// <summary>
/// Preferred outreach channel for customer delight delivery.
/// </summary>
public enum DelightOutreachChannel
{
    WhatsApp = 1,
    Sms = 2,
    Email = 3,
    InAppPush = 4
}

/// <summary>
/// Complete behavioral and transactional profile of a customer.
/// </summary>
public sealed record CustomerEngagementProfile(
    string CustomerId,
    string TenantId,
    CustomerTier Tier,
    int DaysSinceLastOrder,
    double OrderFrequencyPerMonth,
    decimal AverageOrderValueDzd,
    double SupportTicketSentimentScore, // -1.0 (extremely dissatisfied) to +1.0 (delighted)
    double ReturnRateRatio,             // 0.0 to 1.0
    int AppSessionsLast30Days,
    int UnopenedMarketingEmailsCount,
    decimal LifetimeValueDzd
);

/// <summary>
/// Output of Cox/Weibull survival curve churn prediction.
/// </summary>
public sealed record ChurnSurvivalPrediction(
    string CustomerId,
    double HazardScore,
    ChurnRiskLevel RiskLevel,
    int EstimatedDaysUntilChurn,
    double SurvivalProbabilityAt30Days,
    double SurvivalProbabilityAt90Days,
    IReadOnlyList<string> ContributingRiskFactors,
    DateTime CalculatedAtUtc
);

/// <summary>
/// Automated personalized delight intervention package.
/// </summary>
public sealed record DelightAction(
    string ActionId,
    string CustomerId,
    CustomerTier CustomerTier,
    DelightIncentiveType IncentiveType,
    decimal EstimatedCostDzd,
    decimal ExpectedLtvLiftDzd,
    decimal DiscountPercent,
    string? CouponCode,
    DelightOutreachChannel Channel,
    DelightActionStatus Status,
    DateTime GeneratedAtUtc,
    DateTime? DispatchedAtUtc = null,
    DateTime? RedeemedAtUtc = null
);

/// <summary>
/// Performance statistics for a Thompson Sampling bandit arm.
/// </summary>
public sealed record BanditArmStats(
    string ArmId,
    DelightIncentiveType IncentiveType,
    double AlphaSuccesses,
    double BetaFailures,
    int TotalTrials,
    decimal TotalLtvRetainedDzd,
    double EstimatedConversionRate
);

/// <summary>
/// Churn analysis request payload.
/// </summary>
public sealed record AnalyzeChurnRiskRequest(
    CustomerEngagementProfile Profile
);

/// <summary>
/// Proactive delight dispatch request payload.
/// </summary>
public sealed record DispatchDelightRequest(
    CustomerEngagementProfile Profile,
    bool ForceDispatch = false
);

/// <summary>
/// Offer redemption reporting payload for closed-loop bandit training.
/// </summary>
public sealed record ReportOfferRedemptionRequest(
    string ActionId,
    DelightIncentiveType IncentiveType,
    bool Converted,
    decimal RetainedLtvDzd = 0m
);
