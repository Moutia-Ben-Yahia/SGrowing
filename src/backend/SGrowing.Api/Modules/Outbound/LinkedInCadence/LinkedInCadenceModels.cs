using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Outbound.LinkedInCadence;

/// <summary>
/// Type of LinkedIn touchpoint in a multi-step social selling sequence.
/// </summary>
public enum LinkedInTouchType
{
    ProfileView,
    ConnectionRequest,
    PersonalizedFollowUp,
    InMailMessage,
    PostEngagement
}

/// <summary>
/// Execution status of an individual prospect touchpoint.
/// </summary>
public enum CadenceStepStatus
{
    Scheduled,
    PendingApproval,
    Sent,
    Replied,
    Skipped
}

/// <summary>
/// Prospect hierarchy level used for AI persona adaptation and pacing rules.
/// </summary>
public enum ProspectSeniority
{
    CXO,
    VP,
    Director,
    Manager,
    IndividualContributor
}

/// <summary>
/// Enriched B2B prospect profile for LinkedIn outreach.
/// </summary>
public sealed record LinkedInProspectProfile(
    Guid ProspectId,
    string FullName,
    string JobTitle,
    string CompanyName,
    ProspectSeniority Seniority,
    string LinkedInProfileUrl,
    string Location,
    string Industry,
    bool Connected,
    string? CompanyDomain = null,
    string? EnrichedBio = null,
    IReadOnlyList<string>? RecentInterests = null);

/// <summary>
/// Step template definition for configuring multi-touch cadence workflows.
/// </summary>
public sealed record CreateCadenceStepDto(
    int DayOffset,
    LinkedInTouchType TouchType,
    string MessageTemplate,
    int PacingDelaySeconds = 90);

/// <summary>
/// An actionable step instance assigned to a prospect within a cadence.
/// </summary>
public sealed record LinkedInCadenceStepRecord(
    Guid StepId,
    Guid CadenceId,
    Guid ProspectId,
    LinkedInProspectProfile? Prospect,
    int DayOffset,
    LinkedInTouchType TouchType,
    CadenceStepStatus Status,
    string MessageTemplate,
    string? CustomizedContent,
    DateTime? ExecutionTimestampUtc,
    int PacingDelaySeconds,
    double AiPersonalizationScore,
    string? ResponseReceived = null);

/// <summary>
/// Campaign cadence container orchestrating multi-channel social selling.
/// </summary>
public sealed record LinkedInCampaignCadence(
    Guid CadenceId,
    Guid TenantId,
    string Name,
    string TargetPersona,
    string Description,
    bool IsActive,
    int DailyActionLimit,
    DateTime CreatedAtUtc,
    IReadOnlyList<LinkedInCadenceStepRecord> Steps,
    int ActiveProspectCount,
    double ConnectionRatePercent,
    double ReplyRatePercent);

/// <summary>
/// Request payload to create a new outreach cadence.
/// </summary>
public sealed record CreateCadenceRequest(
    Guid TenantId,
    string Name,
    string TargetPersona,
    string Description,
    int DailyActionLimit = 50,
    IReadOnlyList<CreateCadenceStepDto>? InitialSteps = null);

/// <summary>
/// Request payload to enroll an enriched prospect into an existing cadence.
/// </summary>
public sealed record AddProspectToCadenceRequest(
    Guid TenantId,
    Guid CadenceId,
    LinkedInProspectProfile Prospect,
    int StartingDayOffset = 1);

/// <summary>
/// Request payload to execute or approve a scheduled cadence touchpoint.
/// </summary>
public sealed record ExecuteCadenceStepRequest(
    Guid TenantId,
    Guid CadenceId,
    Guid StepId,
    string? ApprovedContent = null,
    bool MarkAsReplied = false);

/// <summary>
/// Request payload to draft AI-personalized messaging tailored for GCC/B2B context.
/// </summary>
public sealed record DraftMessageAiRequest(
    Guid TenantId,
    LinkedInTouchType TouchType,
    string ProspectName,
    string CompanyName,
    string JobTitle,
    ProspectSeniority Seniority,
    string Industry,
    string? KeyPainPoint = null,
    string Language = "en");

/// <summary>
/// Result of AI-driven message generation with pacing and conversion recommendations.
/// </summary>
public sealed record DraftMessageAiResponse(
    string? DraftedSubject,
    string DraftedMessage,
    LinkedInTouchType TouchType,
    string PersonalizationHook,
    int PacingWaitMinutes,
    double ConfidenceScore,
    IReadOnlyList<string> RecommendedHashtags);

/// <summary>
/// Executive overview report of LinkedIn outreach performance and anti-bot pacing queue health.
/// </summary>
public sealed record LinkedInCadenceOverviewReport(
    Guid TenantId,
    int TotalCadences,
    int ActiveProspectsCount,
    int TotalStepsExecuted,
    int PendingApprovalCount,
    double AvgConnectionRatePercent,
    double AvgReplyRatePercent,
    string PacingQueueStatus,
    int DailyQuotaUsed,
    int DailyQuotaLimit,
    IReadOnlyList<LinkedInCadenceStepRecord> RecentSteps);
