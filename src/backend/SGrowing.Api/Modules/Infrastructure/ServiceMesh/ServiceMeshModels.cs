namespace SGrowing.Api.Modules.Infrastructure.ServiceMesh;

public enum CircuitState
{
    Closed,
    HalfOpen,
    Open,
    Isolated
}

public enum CanaryDecision
{
    KeepProbing,
    PromoteCanary,
    RollbackCanary
}

public sealed record UpstreamInstance(
    string InstanceId,
    string ServiceName,
    string HostAddress,
    int Port,
    CircuitState State = CircuitState.Closed,
    int ConsecutiveFailures = 0,
    long TotalCalls = 0,
    long FailedCalls = 0,
    double LatencyEwmaMs = 15.0,
    DateTime? EjectedUntilUtc = null,
    int TotalEjections = 0
);

public sealed record CanaryPolicy(
    string ServiceName,
    string BaselineVersion, // e.g. "v1.2.0"
    string CanaryVersion,   // e.g. "v2.0.0-rc1"
    double CanaryTrafficRatio, // 0.10 (10%)
    double MaxAllowedErrorDeltaPercent = 2.0, // 2% higher error triggers rollback
    double MaxAllowedLatencyDeltaPercent = 25.0 // 25% slower p95 triggers rollback
);

public sealed record CanaryEvaluationReport(
    string ServiceName,
    string BaselineVersion,
    string CanaryVersion,
    long BaselineTotalCalls,
    double BaselineErrorRatePercent,
    double BaselineP95LatencyMs,
    long CanaryTotalCalls,
    double CanaryErrorRatePercent,
    double CanaryP95LatencyMs,
    double ErrorDeltaPercent,
    double LatencyDeltaPercent,
    CanaryDecision Decision,
    string Reason,
    DateTime EvaluatedAtUtc
);

public sealed record RateLimitResult(
    string TenantId,
    string ServiceKey,
    bool Allowed,
    double RemainingTokens,
    double BucketCapacity,
    TimeSpan RetryAfter
);

public sealed record ServiceInvocationResult(
    string InstanceId,
    bool Success,
    int HttpStatusCode,
    double LatencyMs
);
