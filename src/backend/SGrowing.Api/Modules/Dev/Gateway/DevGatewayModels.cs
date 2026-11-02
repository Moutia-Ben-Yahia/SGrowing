using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Dev.Gateway;

public enum DeveloperTier
{
    Starter,
    Growth,
    Enterprise,
    Partner
}

public sealed record TierQuotaInfo(
    DeveloperTier Tier,
    int RequestsPerMinute,
    int BurstCapacity,
    bool SandboxAllowed,
    bool WebhookReplayAllowed,
    int MaxWebhooks
);

public sealed record RateLimitCheckRequest(
    string ApiKey,
    string TenantId,
    DeveloperTier Tier,
    int RequestedTokens = 1
);

public sealed record RateLimitCheckResult(
    bool IsAllowed,
    int RemainingTokens,
    int Capacity,
    double RefillRatePerSecond,
    double RetryAfterSeconds,
    DeveloperTier Tier,
    DateTime CheckedAtUtc
);

public sealed record SandboxSessionConfig(
    string TenantId,
    bool IsSandboxMode,
    int SimulatedLatencyMs,
    bool SimulateFailures,
    double FailureRatePercent,
    List<string> MockHeaders
);

public sealed record SandboxSimulationRequest(
    string TenantId,
    string Endpoint,
    string HttpMethod,
    string? PayloadJson,
    bool ForceError = false
);

public sealed record SandboxSimulationResponse(
    int StatusCode,
    string ResponsePayload,
    int LatencyMs,
    bool IsSandbox,
    string TraceId,
    DateTime ExecutedAtUtc
);

public sealed record OpenApiExportQuery(
    string TargetRole = "public",
    bool IncludeSchemas = true,
    string Format = "json"
);

public sealed record OpenApiExportResult(
    string Title,
    string Version,
    string TargetRole,
    int EndpointsCount,
    Dictionary<string, object> Spec,
    DateTime GeneratedAtUtc
);
