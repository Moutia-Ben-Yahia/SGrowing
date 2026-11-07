using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.AI.Evaluation;

/// <summary>
/// Autonomous agent operational readiness levels across supervised and autonomous execution phases.
/// </summary>
public enum AutonomyReadinessLevel
{
    /// <summary>
    /// Agent observes, analyzes, and inspects data without emitting modifications or drafts.
    /// </summary>
    Read,

    /// <summary>
    /// Agent drafts creative, analytical, or workflow changes for human review and iteration.
    /// </summary>
    Draft,

    /// <summary>
    /// Supervised mode: agent queues high-confidence actions requiring human or gatekeeper sign-off.
    /// </summary>
    Approve,

    /// <summary>
    /// Full autonomous execution: high precision and zero or near-zero hallucination rate.
    /// </summary>
    Execute
}

/// <summary>
/// Quality regression trajectory tracked across evaluation epochs and model/prompt releases.
/// </summary>
public enum RegressionTrend
{
    /// <summary>
    /// Composite evaluation scores and tool reliability improved compared to preceding baseline.
    /// </summary>
    Improving,

    /// <summary>
    /// Operational vitals remain within calibrated variance tolerances (steady-state).
    /// </summary>
    Stable,

    /// <summary>
    /// Quality metric degradation, elevated hallucination rate, or human rejection spikes detected.
    /// </summary>
    Regressing
}

/// <summary>
/// Immutable point-in-time benchmark evaluation record for an autonomous agent run.
/// </summary>
public record AgentEvaluationRecord(
    Guid EvaluationId,
    Guid TenantId,
    string AgentRole,
    string ModelVersion,
    string PromptVersion,
    double TaskCompletionRate,
    double ToolPrecisionRate,
    double HumanApprovalRate,
    double HallucinationScore,
    decimal CostPerTaskSar,
    double LatencyMs,
    int TotalTasksEvaluated,
    DateTime EvaluatedAtUtc
);

/// <summary>
/// Aggregated multi-axis quality benchmark scorecard for a specific AI agent persona.
/// </summary>
public record AgentRadarScorecard(
    string AgentRole,
    AutonomyReadinessLevel AutonomyLevel,
    double CompositeScore,
    double TaskSuccessRate,
    double ToolPrecisionRate,
    double ApprovalRate,
    double HallucinationIndex,
    decimal CostPerTaskSar,
    double AverageLatencyMs,
    RegressionTrend Trend,
    string Recommendation
);

/// <summary>
/// Tenant-level panoramic evaluation radar report summarizing global fleet health and autonomy readiness.
/// </summary>
public record GlobalEvaluationRadarReport(
    double GlobalScore,
    int TotalTasksEvaluated,
    int FullyAutonomousAgentCount,
    int SupervisedAgentCount,
    decimal AverageCostPerTaskSar,
    List<string> TopRejectionReasons,
    List<AgentRadarScorecard> AgentScorecards,
    DateTime GeneratedAtUtc
);

/// <summary>
/// Inbound payload to log a single agent task execution or human review verdict into the quality registry.
/// </summary>
public record RecordEvaluationRequest(
    Guid TenantId,
    string AgentRole,
    string ModelVersion,
    string PromptVersion,
    bool WasSuccessful,
    bool ToolExecutionValid,
    bool HumanApproved,
    bool HadHallucination,
    decimal CostSar,
    double LatencyMs,
    string? RejectionReason = null
);

/// <summary>
/// Autonomous promotion/demotion diagnostic verdict detailing safety checks and policy decisions.
/// </summary>
public record AutonomyPromotionCheckResult(
    string AgentRole,
    AutonomyReadinessLevel CurrentLevel,
    AutonomyReadinessLevel RecommendedLevel,
    bool LevelChanged,
    string Reason,
    AgentRadarScorecard Scorecard
);
