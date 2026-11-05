using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using SGrowing.Core.Entities;

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

public interface IDevGatewayPersistenceStore
{
    void SaveSandboxSession(SandboxSessionConfig config);
    SandboxSessionConfig? GetSandboxSession(string tenantId);
    void SaveTokenBucket(string bucketKey, double currentTokens, DateTime lastRefillUtc);
    (double CurrentTokens, DateTime LastRefillUtc)? GetTokenBucket(string bucketKey);
}

public sealed class InMemoryDurableDevGatewayPersistenceStore : IDevGatewayPersistenceStore
{
    private readonly ConcurrentDictionary<string, SandboxSessionConfig> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (double CurrentTokens, DateTime LastRefillUtc)> _buckets = new(StringComparer.OrdinalIgnoreCase);

    public void SaveSandboxSession(SandboxSessionConfig config) => _sessions[config.TenantId] = config;
    public SandboxSessionConfig? GetSandboxSession(string tenantId) => _sessions.TryGetValue(tenantId, out var c) ? c : null;
    public void SaveTokenBucket(string bucketKey, double currentTokens, DateTime lastRefillUtc) => _buckets[bucketKey] = (currentTokens, lastRefillUtc);
    public (double CurrentTokens, DateTime LastRefillUtc)? GetTokenBucket(string bucketKey) => _buckets.TryGetValue(bucketKey, out var b) ? b : null;
}

public static class DevSandboxSessionEntityExtensions
{
    public static SandboxSessionConfig ToConfig(this DevSandboxSessionEntity entity)
    {
        List<string>? headers = null;
        if (!string.IsNullOrWhiteSpace(entity.MockHeadersJson))
        {
            try
            {
                headers = JsonSerializer.Deserialize<List<string>>(entity.MockHeadersJson);
            }
            catch
            {
                // Fallback to empty list on corrupted json
            }
        }

        return new SandboxSessionConfig(
            TenantId: entity.TenantId,
            IsSandboxMode: entity.IsSandboxMode,
            SimulatedLatencyMs: entity.SimulatedLatencyMs,
            SimulateFailures: entity.SimulateFailures,
            FailureRatePercent: entity.FailureRatePercent,
            MockHeaders: headers ?? new List<string>()
        );
    }

    public static DevSandboxSessionEntity ToEntity(this SandboxSessionConfig config)
    {
        return new DevSandboxSessionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = config.TenantId,
            IsSandboxMode = config.IsSandboxMode,
            SimulatedLatencyMs = config.SimulatedLatencyMs,
            SimulateFailures = config.SimulateFailures,
            FailureRatePercent = config.FailureRatePercent,
            MockHeadersJson = JsonSerializer.Serialize(config.MockHeaders ?? new List<string>()),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
