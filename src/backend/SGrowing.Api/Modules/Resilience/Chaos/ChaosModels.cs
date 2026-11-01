namespace SGrowing.Api.Modules.Resilience.Chaos;

/// <summary>
/// Downstream system dependency targeted by chaos fault injection.
/// </summary>
public enum ChaosTarget
{
    Database,
    Redis,
    LogisticsCarrierApi,
    WhatsAppGateway,
    VoiceTelephony,
    PaymentGateway
}

/// <summary>
/// Type of synthetic failure injected into downstream communications.
/// </summary>
public enum ChaosFaultType
{
    LatencyDelay,
    TransientIoException,
    DatabaseConnectionDrop,
    RedisCacheMissStorm,
    CarrierHttp503Error,
    Http429TooManyRequests,
    PayloadCorruption
}

/// <summary>
/// Lifecycle status of an automated chaos experiment.
/// </summary>
public enum ExperimentStatus
{
    Idle,
    Running,
    Aborted,
    Completed
}

/// <summary>
/// SLA resilience grading evaluated by the Resilience Autopilot Coordinator.
/// </summary>
public enum ResilienceGrade
{
    GradeAPlus, // 100% fallback success rate and 0 unhandled failures
    GradeA,     // >= 98% fallback success rate
    GradeB,     // >= 90% fallback success rate
    GradeF      // < 90% fallback success rate or aborted due to SLA violation
}

/// <summary>
/// Defines an active chaos fault rule applied to downstream operations.
/// </summary>
/// <param name="RuleId">Unique rule identifier.</param>
/// <param name="Target">Target dependency.</param>
/// <param name="FaultType">Category of fault injected.</param>
/// <param name="Probability">Trigger probability between 0.0 and 1.0.</param>
/// <param name="LatencyDelayMs">Delay in milliseconds if LatencyDelay is selected.</param>
/// <param name="ErrorStatusCode">HTTP or error status code to simulate.</param>
/// <param name="ErrorMessage">Custom error message returned or thrown.</param>
/// <param name="Enabled">Whether the rule is actively evaluating traffic.</param>
/// <param name="CreatedAt">Timestamp when rule was registered.</param>
/// <param name="ExpiresAt">Optional expiration timestamp after which rule is ignored.</param>
public record ChaosFaultRule(
    string RuleId,
    ChaosTarget Target,
    ChaosFaultType FaultType,
    double Probability,
    int LatencyDelayMs = 0,
    int? ErrorStatusCode = null,
    string? ErrorMessage = null,
    bool Enabled = true,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Request payload to add a new chaos fault rule.
/// </summary>
public record AddFaultRuleRequest(
    ChaosTarget Target,
    ChaosFaultType FaultType,
    double Probability = 1.0,
    int LatencyDelayMs = 0,
    int? ErrorStatusCode = null,
    string? ErrorMessage = null,
    int? TtlSeconds = null,
    bool Enabled = true);

/// <summary>
/// Request payload to launch an autonomous chaos experiment.
/// </summary>
/// <param name="ExperimentName">Descriptive name of the experiment.</param>
/// <param name="Target">Target dependency.</param>
/// <param name="FaultType">Fault type to inject.</param>
/// <param name="DurationSeconds">Planned experiment run duration.</param>
/// <param name="FaultProbability">Probability of fault triggering on canary calls.</param>
/// <param name="MaxAllowedErrorRate">Error rate threshold above which experiment is aborted (e.g. 0.05 for 5%).</param>
/// <param name="CanaryTrafficPercentage">Canary traffic percentage capped at 1.0% blast-radius limit.</param>
public record StartExperimentRequest(
    string ExperimentName,
    ChaosTarget Target,
    ChaosFaultType FaultType,
    int DurationSeconds,
    double FaultProbability,
    double MaxAllowedErrorRate,
    double CanaryTrafficPercentage);

/// <summary>
/// Request payload to abort an active experiment manually.
/// </summary>
public record AbortExperimentRequest(string? Reason = null);

/// <summary>
/// Comprehensive post-experiment or live status report for an experiment.
/// </summary>
public record ChaosExperimentReport(
    string ExperimentId,
    string ExperimentName,
    ChaosTarget Target,
    ChaosFaultType FaultType,
    ExperimentStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int DurationSeconds,
    int TotalRequestsInjected,
    int FallbackSuccessCount,
    int FailedRequestsCount,
    double FallbackSuccessRate,
    double ObservedP99LatencyMs,
    ResilienceGrade ResilienceGrade,
    string? AbortReason);

/// <summary>
/// Audit status evaluating circuit breaker degradation posture for a dependency.
/// </summary>
public record CircuitBreakerAuditStatus(
    ChaosTarget Target,
    int TotalExecutions,
    int InjectedFailures,
    int FallbacksTriggered,
    double FallbackSuccessRate,
    bool Healthy);

/// <summary>
/// Real-time metrics counters for injected faults across the mesh.
/// </summary>
public record InjectedMetrics(
    long TotalFaultsInjected,
    long LatencyInjections,
    long ExceptionInjections,
    int ActiveRulesCount);

/// <summary>
/// High-level Autopilot engine status including remaining SLA error budget.
/// </summary>
public record AutopilotStatus(
    string? ActiveExperimentId,
    bool IsRunning,
    int TotalExperimentsRun,
    double SystemErrorBudgetRemaining,
    ResilienceGrade? LastAuditGrade);
