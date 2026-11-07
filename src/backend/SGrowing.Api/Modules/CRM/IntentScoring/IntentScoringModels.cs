using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.CRM.IntentScoring;

#region Domain Enums

/// <summary>
/// Ideal Customer Profile (ICP) grade based on firmographic fit score.
/// Grade A: 90-100 fit
/// Grade B: 75-89 fit
/// Grade C: 50-74 fit
/// Grade D: < 50 fit
/// </summary>
public enum IcpGrade
{
    GradeA,
    GradeB,
    GradeC,
    GradeD
}

/// <summary>
/// Categorized intent signals captured across first-party surfaces and third-party surge networks.
/// </summary>
public enum IntentSignalType
{
    // First-Party Behavioral Signals
    PricingPageView,
    DocumentationView,
    WhitepaperDownload,
    WebinarAttendance,
    DemoRequest,
    FreeTrialSignup,
    ProductFeatureUsage,
    EmailEngagement,

    // Third-Party Intent & Surge Signals
    BomboraSurgeTopic,
    G2CompetitorComparison,
    G2CategorySearch,
    JobPostingExpansion,
    FundingAnnouncement,
    ExecutiveHire
}

/// <summary>
/// High-level source origin of an intent or fit signal.
/// </summary>
public enum SignalSource
{
    FirstParty,
    ThirdPartySurge,
    Technographic,
    Firmographic
}

/// <summary>
/// Predictive buying stage of the prospect based on decayed multi-signal scores.
/// </summary>
public enum PredictiveBuyingStage
{
    Cold,
    Awareness,
    Consideration,
    HighIntent,
    ReadyToBuy
}

/// <summary>
/// CRM qualification stage with automated state transitions.
/// </summary>
public enum LeadQualificationStage
{
    Unqualified,
    MarketingQualifiedLead,
    SalesQualifiedLead
}

/// <summary>
/// Recommended next step for revenue teams or automated orchestration engines.
/// </summary>
public enum NextActionRecommendation
{
    ScheduleExecutiveBriefing,
    RouteToSeniorSdrImmediate,
    EnrollInHighIntentCadence,
    TriggerAutomatedTechnicalNurture,
    MonitorAndLowTouchNewsletter,
    DisqualifyOrSuppress
}

#endregion

#region Domain Records & Models

/// <summary>
/// An atomic buying intent signal ingested into the scoring engine.
/// </summary>
public record IntentSignal(
    Guid SignalId,
    IntentSignalType Type,
    SignalSource Source,
    string TopicOrResource,
    double RawScore,
    double Weight,
    DateTime OccurredAtUtc,
    Dictionary<string, string>? Metadata = null
);

/// <summary>
/// Firmographic characteristics of the prospect company and ICP fit score.
/// </summary>
public record FirmographicProfile(
    string Domain,
    string CompanyName,
    string Industry,
    string EmployeeCountTier,
    string AnnualRevenueTier,
    string HeadquartersRegion,
    int FoundedYear,
    double FitScore,
    IcpGrade Grade
);

/// <summary>
/// Technographic stack profile and category compatibility score.
/// </summary>
public record TechnographicProfile(
    IReadOnlyList<string> InstalledStack,
    IReadOnlyList<string> CompatibleCategories,
    bool HasCompetitorStack,
    double TechnographicScore
);

/// <summary>
/// Tenant-level scoring configuration with weights, decay half-life, and qualification thresholds.
/// </summary>
public record IntentScoringConfig(
    Guid TenantId,
    double WeightFirmographic,
    double WeightTechnographic,
    double WeightFirstPartyBehavioral,
    double WeightThirdPartySurge,
    int DecayHalfLifeDays,
    double MqlThresholdScore,
    double SqlThresholdScore,
    IReadOnlyList<string> TargetIndustries,
    IReadOnlyList<string> TargetEmployeeTiers,
    IReadOnlyList<string> TargetRegions,
    DateTime UpdatedAtUtc
);

/// <summary>
/// Detailed breakdown of weighted components comprising the composite intent score.
/// </summary>
public record ScoreBreakdown(
    double FirmographicWeight,
    double FirmographicScore,
    double FirmographicWeighted,
    double TechnographicWeight,
    double TechnographicScore,
    double TechnographicWeighted,
    double FirstPartyBehavioralWeight,
    double FirstPartyBehavioralRawScore,
    double FirstPartyBehavioralDecayedScore,
    double FirstPartyBehavioralWeighted,
    double ThirdPartySurgeWeight,
    double ThirdPartySurgeRawScore,
    double ThirdPartySurgeDecayedScore,
    double ThirdPartySurgeWeighted,
    double CompositeScore
);

/// <summary>
/// Comprehensive state of an evaluated lead with intent history and next action recommendations.
/// </summary>
public record LeadIntentRecord(
    Guid LeadId,
    Guid TenantId,
    string ContactEmail,
    string ContactName,
    string JobTitle,
    FirmographicProfile Firmographics,
    TechnographicProfile Technographics,
    IReadOnlyList<IntentSignal> ActiveSignals,
    ScoreBreakdown ScoreBreakdown,
    double CompositeScore,
    IcpGrade IcpGrade,
    PredictiveBuyingStage BuyingStage,
    LeadQualificationStage QualificationStage,
    NextActionRecommendation RecommendedAction,
    string ActionReasoning,
    DateTime LastEvaluatedAtUtc,
    DateTime CreatedAtUtc
);

#endregion

#region Request & Response DTOs

/// <summary>
/// Ingestion payload for registering an intent signal for a lead.
/// </summary>
public record IngestSignalRequest(
    Guid LeadId,
    IntentSignalType Type,
    SignalSource Source,
    string TopicOrResource,
    double RawScore,
    double Weight = 1.0,
    DateTime? OccurredAtUtc = null,
    Dictionary<string, string>? Metadata = null,
    Guid? TenantId = null
);

/// <summary>
/// Upsert payload for a lead profile.
/// </summary>
public record CreateOrUpdateLeadIntentRequest(
    Guid? LeadId,
    Guid? TenantId,
    string ContactEmail,
    string ContactName,
    string JobTitle,
    FirmographicProfile Firmographics,
    TechnographicProfile Technographics
);

/// <summary>
/// Configuration update payload for tenant scoring parameters.
/// </summary>
public record UpdateIntentConfigDto(
    double WeightFirmographic,
    double WeightTechnographic,
    double WeightFirstPartyBehavioral,
    double WeightThirdPartySurge,
    int DecayHalfLifeDays,
    double MqlThresholdScore,
    double SqlThresholdScore,
    IReadOnlyList<string>? TargetIndustries = null,
    IReadOnlyList<string>? TargetEmployeeTiers = null,
    IReadOnlyList<string>? TargetRegions = null
);

/// <summary>
/// Request to trigger batch recalculation of all lead scores for a tenant.
/// </summary>
public record BatchRecalculateRequest(
    Guid? TenantId = null
);

/// <summary>
/// Result summary of batch scoring recalculation.
/// </summary>
public record BatchRecalculateResponse(
    int ProcessedCount,
    int SqlCount,
    int MqlCount,
    int UnqualifiedCount,
    double AverageScore,
    DateTime RecalculatedAtUtc
);

/// <summary>
/// Executive overview and radar metrics for intent scoring across the tenant.
/// </summary>
public record IntentScoringOverviewDto(
    int TotalLeads,
    int SqlCount,
    int MqlCount,
    int UnqualifiedCount,
    int GradeACount,
    int GradeBCount,
    int GradeCCount,
    int GradeDCount,
    double AverageCompositeScore,
    IReadOnlyList<LeadIntentRecord> TopSurgingLeads,
    IntentScoringConfig ActiveConfig,
    DateTime ComputedAtUtc
);

#endregion
