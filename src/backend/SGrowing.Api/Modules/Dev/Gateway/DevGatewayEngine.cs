using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Dev.Gateway;

public interface IDevGatewayEngine
{
    RateLimitCheckResult EvaluateRateLimit(RateLimitCheckRequest request);
    TierQuotaInfo GetTierQuota(DeveloperTier tier);
    SandboxSessionConfig ConfigureSandboxSession(SandboxSessionConfig config);
    SandboxSessionConfig? GetSandboxSession(string tenantId);
    SandboxSimulationResponse SimulateRequest(SandboxSimulationRequest request);
    OpenApiExportResult ExportOpenApiSpec(OpenApiExportQuery query);
}

public sealed class DevGatewayEngine : IDevGatewayEngine
{
    private sealed class TokenBucketState
    {
        public double CurrentTokens { get; set; }
        public DateTime LastRefillUtc { get; set; }
        public object SyncLock { get; } = new();
    }

    private readonly ConcurrentDictionary<string, TokenBucketState> _buckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SandboxSessionConfig> _sandboxSessions = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<DeveloperTier, TierQuotaInfo> QuotaMap = new()
    {
        [DeveloperTier.Starter] = new TierQuotaInfo(DeveloperTier.Starter, RequestsPerMinute: 100, BurstCapacity: 25, SandboxAllowed: true, WebhookReplayAllowed: false, MaxWebhooks: 3),
        [DeveloperTier.Growth] = new TierQuotaInfo(DeveloperTier.Growth, RequestsPerMinute: 500, BurstCapacity: 100, SandboxAllowed: true, WebhookReplayAllowed: true, MaxWebhooks: 10),
        [DeveloperTier.Enterprise] = new TierQuotaInfo(DeveloperTier.Enterprise, RequestsPerMinute: 2500, BurstCapacity: 500, SandboxAllowed: true, WebhookReplayAllowed: true, MaxWebhooks: 50),
        [DeveloperTier.Partner] = new TierQuotaInfo(DeveloperTier.Partner, RequestsPerMinute: 10000, BurstCapacity: 2000, SandboxAllowed: true, WebhookReplayAllowed: true, MaxWebhooks: 200)
    };

    public TierQuotaInfo GetTierQuota(DeveloperTier tier)
    {
        return QuotaMap.TryGetValue(tier, out var quota)
            ? quota
            : QuotaMap[DeveloperTier.Starter];
    }

    public RateLimitCheckResult EvaluateRateLimit(RateLimitCheckRequest request)
    {
        var quota = GetTierQuota(request.Tier);
        var bucketKey = $"{request.TenantId}:{request.ApiKey}";
        var now = DateTime.UtcNow;

        var bucket = _buckets.GetOrAdd(bucketKey, _ => new TokenBucketState
        {
            CurrentTokens = quota.BurstCapacity,
            LastRefillUtc = now
        });

        lock (bucket.SyncLock)
        {
            double refillRatePerSecond = quota.RequestsPerMinute / 60.0;
            double elapsedSeconds = Math.Max(0, (now - bucket.LastRefillUtc).TotalSeconds);
            bucket.LastRefillUtc = now;

            bucket.CurrentTokens = Math.Min(quota.BurstCapacity, bucket.CurrentTokens + (elapsedSeconds * refillRatePerSecond));

            int requested = Math.Max(1, request.RequestedTokens);

            if (bucket.CurrentTokens >= requested)
            {
                bucket.CurrentTokens -= requested;
                return new RateLimitCheckResult(
                    IsAllowed: true,
                    RemainingTokens: (int)Math.Floor(bucket.CurrentTokens),
                    Capacity: quota.BurstCapacity,
                    RefillRatePerSecond: Math.Round(refillRatePerSecond, 2),
                    RetryAfterSeconds: 0,
                    Tier: request.Tier,
                    CheckedAtUtc: now
                );
            }
            else
            {
                double shortfall = requested - bucket.CurrentTokens;
                double retryAfterSeconds = Math.Ceiling(shortfall / refillRatePerSecond);

                return new RateLimitCheckResult(
                    IsAllowed: false,
                    RemainingTokens: (int)Math.Floor(bucket.CurrentTokens),
                    Capacity: quota.BurstCapacity,
                    RefillRatePerSecond: Math.Round(refillRatePerSecond, 2),
                    RetryAfterSeconds: Math.Max(1, retryAfterSeconds),
                    Tier: request.Tier,
                    CheckedAtUtc: now
                );
            }
        }
    }

    public SandboxSessionConfig ConfigureSandboxSession(SandboxSessionConfig config)
    {
        _sandboxSessions[config.TenantId] = config;
        return config;
    }

    public SandboxSessionConfig? GetSandboxSession(string tenantId)
    {
        return _sandboxSessions.TryGetValue(tenantId, out var config)
            ? config
            : new SandboxSessionConfig(tenantId, IsSandboxMode: true, SimulatedLatencyMs: 45, SimulateFailures: false, FailureRatePercent: 0.0, new List<string> { "X-SGrowing-Sandbox: true" });
    }

    public SandboxSimulationResponse SimulateRequest(SandboxSimulationRequest request)
    {
        var session = GetSandboxSession(request.TenantId) ?? new SandboxSessionConfig(
            request.TenantId,
            IsSandboxMode: true,
            SimulatedLatencyMs: 25,
            SimulateFailures: false,
            FailureRatePercent: 0.0,
            new List<string>()
        );

        int latency = Math.Max(5, session.SimulatedLatencyMs);
        string traceId = $"sbx_{Guid.NewGuid():N}";
        DateTime now = DateTime.UtcNow;

        if (request.ForceError || (session.SimulateFailures && session.FailureRatePercent >= 50.0))
        {
            return new SandboxSimulationResponse(
                StatusCode: 500,
                ResponsePayload: "{\"error\":\"SimulatedSandboxFailure\",\"message\":\"The sandbox session has forced failure mode enabled.\"}",
                LatencyMs: latency,
                IsSandbox: true,
                TraceId: traceId,
                ExecutedAtUtc: now
            );
        }

        string endpoint = request.Endpoint.ToLowerInvariant();
        string responseJson;

        if (endpoint.Contains("capi") || endpoint.Contains("conversion"))
        {
            responseJson = "{\"status\":\"simulated_success\",\"events_received\":1,\"matched_profiles\":1,\"capi_version\":\"v21.0\",\"dedup_key\":\"sbx_dedup_098\"}";
        }
        else if (endpoint.Contains("identity") || endpoint.Contains("stitch"))
        {
            responseJson = "{\"status\":\"stitched_in_sandbox\",\"cluster_id\":\"sbx_cluster_442\",\"merged_identities\":2}";
        }
        else if (endpoint.Contains("webhook"))
        {
            responseJson = "{\"status\":\"delivered_to_sandbox_sink\",\"delivered\":true,\"simulated_subscriber\":\"https://sandbox-receiver.local/webhook\"}";
        }
        else
        {
            responseJson = $"{{\"status\":\"sandbox_echo\",\"method\":\"{request.HttpMethod}\",\"endpoint\":\"{request.Endpoint}\",\"tenantId\":\"{request.TenantId}\"}}";
        }

        return new SandboxSimulationResponse(
            StatusCode: 200,
            ResponsePayload: responseJson,
            LatencyMs: latency,
            IsSandbox: true,
            TraceId: traceId,
            ExecutedAtUtc: now
        );
    }

    public OpenApiExportResult ExportOpenApiSpec(OpenApiExportQuery query)
    {
        string role = (query.TargetRole ?? "public").ToLowerInvariant();
        var paths = new Dictionary<string, object>();

        // Core Public Endpoints
        paths["/api/v1/customer-data/identity/stitch"] = new Dictionary<string, object>
        {
            ["post"] = new Dictionary<string, object>
            {
                ["summary"] = "Deterministic Customer Identity Graph Resolution & Stitching",
                ["tags"] = new[] { "CDP", "Identity" },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object> { ["description"] = "Cluster identity resolved." }
                }
            }
        };

        paths["/api/v1/advertising/capi/events"] = new Dictionary<string, object>
        {
            ["post"] = new Dictionary<string, object>
            {
                ["summary"] = "High-Throughput Meta/Google Conversions API (CAPI) Ingestion",
                ["tags"] = new[] { "Advertising", "CAPI" },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object> { ["description"] = "CAPI conversion event processed." }
                }
            }
        };

        paths["/api/v1/customer-data/firmographics/deanonymize"] = new Dictionary<string, object>
        {
            ["post"] = new Dictionary<string, object>
            {
                ["summary"] = "B2B Reverse IP Visitor Deanonymization & ICP Scoring",
                ["tags"] = new[] { "CDP", "B2B" },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object> { ["description"] = "Company profile resolved." }
                }
            }
        };

        // Partner / Admin Tier Endpoints
        if (role == "partner" || role == "admin")
        {
            paths["/api/v1/journeys/omnichannel/dispatch"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Omnichannel Sequence Outreach (WhatsApp/Email/SMS/Voice SDR)",
                    ["tags"] = new[] { "Journeys", "Outreach" },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Step dispatched." }
                    }
                }
            };
        }

        if (role == "admin")
        {
            paths["/api/v1/agents/growth-director/decompose"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Supervised AI Growth Director 4-Agent Mission Planner",
                    ["tags"] = new[] { "AI", "Autonomy" },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Objective decomposed into specialist sub-tasks." }
                    }
                }
            };
        }

        var spec = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.3",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "SGrowing AI-Native Growth OS Developer Gateway API",
                ["version"] = "v1.0.0",
                ["description"] = "Production OpenAPI 3.0 specification partitioned for " + role
            },
            ["paths"] = paths
        };

        return new OpenApiExportResult(
            Title: "SGrowing AI-Native Growth OS API",
            Version: "v1.0.0",
            TargetRole: role,
            EndpointsCount: paths.Count,
            Spec: spec,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
