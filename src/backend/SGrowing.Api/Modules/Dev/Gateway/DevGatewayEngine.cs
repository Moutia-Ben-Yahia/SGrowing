using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using StackExchange.Redis;

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

    private sealed record TokenBucketRecord(double Tokens, DateTime RefillUtc);

    private readonly ConcurrentDictionary<string, TokenBucketState> _buckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SandboxSessionConfig> _sandboxSessions = new(StringComparer.OrdinalIgnoreCase);

    private readonly IConnectionMultiplexer? _redis;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly SGrowingDbContext? _dbContext;
    private readonly IDevGatewayPersistenceStore? _persistenceStore;
    private readonly ILogger<DevGatewayEngine>? _logger;
    private readonly object _dbLock = new();

    private static readonly Dictionary<DeveloperTier, TierQuotaInfo> QuotaMap = new()
    {
        [DeveloperTier.Starter] = new TierQuotaInfo(DeveloperTier.Starter, RequestsPerMinute: 100, BurstCapacity: 25, SandboxAllowed: true, WebhookReplayAllowed: false, MaxWebhooks: 3),
        [DeveloperTier.Growth] = new TierQuotaInfo(DeveloperTier.Growth, RequestsPerMinute: 500, BurstCapacity: 100, SandboxAllowed: true, WebhookReplayAllowed: true, MaxWebhooks: 10),
        [DeveloperTier.Enterprise] = new TierQuotaInfo(DeveloperTier.Enterprise, RequestsPerMinute: 2500, BurstCapacity: 500, SandboxAllowed: true, WebhookReplayAllowed: true, MaxWebhooks: 50),
        [DeveloperTier.Partner] = new TierQuotaInfo(DeveloperTier.Partner, RequestsPerMinute: 10000, BurstCapacity: 2000, SandboxAllowed: true, WebhookReplayAllowed: true, MaxWebhooks: 200)
    };

    public DevGatewayEngine()
        : this(null, null, null, null, null)
    {
    }

    public DevGatewayEngine(
        IConnectionMultiplexer? redis = null,
        IServiceScopeFactory? scopeFactory = null,
        SGrowingDbContext? dbContext = null,
        IDevGatewayPersistenceStore? persistenceStore = null,
        ILogger<DevGatewayEngine>? logger = null)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _dbContext = dbContext;
        _persistenceStore = persistenceStore;
        _logger = logger;

        WarmupFromPersistence();
    }

    public DevGatewayEngine(IDevGatewayPersistenceStore persistenceStore)
        : this(null, null, null, persistenceStore, null)
    {
    }

    public DevGatewayEngine(SGrowingDbContext dbContext)
        : this(null, null, dbContext, null, null)
    {
    }

    private void WarmupFromPersistence()
    {
        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    LoadFromDbContext(_dbContext);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    LoadFromDbContext(db);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to warm up DevGatewayEngine cache from DbContext");
        }
    }

    private void LoadFromDbContext(SGrowingDbContext db)
    {
        var sessions = db.DevSandboxSessions.ToList();
        foreach (var entity in sessions)
        {
            _sandboxSessions[entity.TenantId] = entity.ToConfig();
        }

        var buckets = db.DevRateLimitBuckets.ToList();
        foreach (var entity in buckets)
        {
            _buckets[entity.BucketKey] = new TokenBucketState
            {
                CurrentTokens = entity.CurrentTokens,
                LastRefillUtc = entity.LastRefillUtc
            };
        }
    }

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

        var bucket = _buckets.GetOrAdd(bucketKey, key =>
        {
            var loaded = LoadBucketFromDurableStore(key);
            if (loaded.HasValue)
            {
                return new TokenBucketState
                {
                    CurrentTokens = loaded.Value.CurrentTokens,
                    LastRefillUtc = loaded.Value.LastRefillUtc
                };
            }

            return new TokenBucketState
            {
                CurrentTokens = quota.BurstCapacity,
                LastRefillUtc = now
            };
        });

        lock (bucket.SyncLock)
        {
            double refillRatePerSecond = quota.RequestsPerMinute / 60.0;
            double elapsedSeconds = Math.Max(0, (now - bucket.LastRefillUtc).TotalSeconds);
            bucket.LastRefillUtc = now;

            bucket.CurrentTokens = Math.Min(quota.BurstCapacity, bucket.CurrentTokens + (elapsedSeconds * refillRatePerSecond));

            int requested = Math.Max(1, request.RequestedTokens);

            bool isAllowed = bucket.CurrentTokens >= requested;
            double retryAfterSeconds = 0;

            if (isAllowed)
            {
                bucket.CurrentTokens -= requested;
            }
            else
            {
                double shortfall = requested - bucket.CurrentTokens;
                retryAfterSeconds = Math.Max(1, Math.Ceiling(shortfall / refillRatePerSecond));
            }

            PersistBucketState(bucketKey, bucket.CurrentTokens, bucket.LastRefillUtc);

            return new RateLimitCheckResult(
                IsAllowed: isAllowed,
                RemainingTokens: (int)Math.Floor(bucket.CurrentTokens),
                Capacity: quota.BurstCapacity,
                RefillRatePerSecond: Math.Round(refillRatePerSecond, 2),
                RetryAfterSeconds: retryAfterSeconds,
                Tier: request.Tier,
                CheckedAtUtc: now
            );
        }
    }

    private (double CurrentTokens, DateTime LastRefillUtc)? LoadBucketFromDurableStore(string bucketKey)
    {
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = db.StringGet($"devgw:ratelimit:{bucketKey}");
                if (val.HasValue)
                {
                    var parsed = JsonSerializer.Deserialize<TokenBucketRecord>(val.ToString());
                    if (parsed != null)
                    {
                        return (parsed.Tokens, parsed.RefillUtc);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read rate-limit bucket {BucketKey} from Redis", bucketKey);
            }
        }

        if (_persistenceStore != null)
        {
            try
            {
                var state = _persistenceStore.GetTokenBucket(bucketKey);
                if (state.HasValue)
                {
                    return state;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read rate-limit bucket {BucketKey} from PersistenceStore", bucketKey);
            }
        }

        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    var entity = _dbContext.DevRateLimitBuckets.FirstOrDefault(b => b.BucketKey == bucketKey);
                    if (entity != null)
                    {
                        return (entity.CurrentTokens, entity.LastRefillUtc);
                    }
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    var entity = db.DevRateLimitBuckets.FirstOrDefault(b => b.BucketKey == bucketKey);
                    if (entity != null)
                    {
                        return (entity.CurrentTokens, entity.LastRefillUtc);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read rate-limit bucket {BucketKey} from DbContext", bucketKey);
        }

        return null;
    }

    private void PersistBucketState(string bucketKey, double currentTokens, DateTime lastRefillUtc)
    {
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var payload = JsonSerializer.Serialize(new TokenBucketRecord(currentTokens, lastRefillUtc));
                db.StringSet($"devgw:ratelimit:{bucketKey}", payload, TimeSpan.FromHours(24), flags: CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to persist rate-limit bucket {BucketKey} to Redis", bucketKey);
            }
        }

        try
        {
            _persistenceStore?.SaveTokenBucket(bucketKey, currentTokens, lastRefillUtc);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist rate-limit bucket {BucketKey} to PersistenceStore", bucketKey);
        }

        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    SaveBucketEntityToDb(_dbContext, bucketKey, currentTokens, lastRefillUtc);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    SaveBucketEntityToDb(db, bucketKey, currentTokens, lastRefillUtc);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist rate-limit bucket {BucketKey} to DbContext", bucketKey);
        }
    }

    private static void SaveBucketEntityToDb(SGrowingDbContext db, string bucketKey, double currentTokens, DateTime lastRefillUtc)
    {
        var entity = db.DevRateLimitBuckets.FirstOrDefault(b => b.BucketKey == bucketKey);
        if (entity == null)
        {
            entity = new DevRateLimitBucketEntity
            {
                Id = Guid.NewGuid(),
                BucketKey = bucketKey,
                CurrentTokens = currentTokens,
                LastRefillUtc = lastRefillUtc,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.DevRateLimitBuckets.Add(entity);
        }
        else
        {
            entity.CurrentTokens = currentTokens;
            entity.LastRefillUtc = lastRefillUtc;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        db.SaveChanges();
    }

    public SandboxSessionConfig ConfigureSandboxSession(SandboxSessionConfig config)
    {
        _sandboxSessions[config.TenantId] = config;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(config);
                db.StringSet($"devgw:sandbox:{config.TenantId}", json, TimeSpan.FromDays(30), flags: CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to persist sandbox session {TenantId} to Redis", config.TenantId);
            }
        }

        try
        {
            _persistenceStore?.SaveSandboxSession(config);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist sandbox session {TenantId} to PersistenceStore", config.TenantId);
        }

        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    SaveSandboxSessionToDb(_dbContext, config);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    SaveSandboxSessionToDb(db, config);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist sandbox session {TenantId} to DbContext", config.TenantId);
        }

        return config;
    }

    private static void SaveSandboxSessionToDb(SGrowingDbContext db, SandboxSessionConfig config)
    {
        var entity = db.DevSandboxSessions.FirstOrDefault(s => s.TenantId == config.TenantId);
        if (entity == null)
        {
            entity = config.ToEntity();
            db.DevSandboxSessions.Add(entity);
        }
        else
        {
            entity.IsSandboxMode = config.IsSandboxMode;
            entity.SimulatedLatencyMs = config.SimulatedLatencyMs;
            entity.SimulateFailures = config.SimulateFailures;
            entity.FailureRatePercent = config.FailureRatePercent;
            entity.MockHeadersJson = JsonSerializer.Serialize(config.MockHeaders ?? new List<string>());
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        db.SaveChanges();
    }

    public SandboxSessionConfig? GetSandboxSession(string tenantId)
    {
        if (_sandboxSessions.TryGetValue(tenantId, out var config))
        {
            return config;
        }

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = db.StringGet($"devgw:sandbox:{tenantId}");
                if (val.HasValue)
                {
                    var loaded = JsonSerializer.Deserialize<SandboxSessionConfig>(val.ToString());
                    if (loaded != null)
                    {
                        _sandboxSessions[tenantId] = loaded;
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load sandbox session {TenantId} from Redis", tenantId);
            }
        }

        if (_persistenceStore != null)
        {
            try
            {
                var loaded = _persistenceStore.GetSandboxSession(tenantId);
                if (loaded != null)
                {
                    _sandboxSessions[tenantId] = loaded;
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load sandbox session {TenantId} from PersistenceStore", tenantId);
            }
        }

        try
        {
            DevSandboxSessionEntity? entity = null;
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    entity = _dbContext.DevSandboxSessions.FirstOrDefault(s => s.TenantId == tenantId);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    entity = db.DevSandboxSessions.FirstOrDefault(s => s.TenantId == tenantId);
                }
            }

            if (entity != null)
            {
                var loaded = entity.ToConfig();
                _sandboxSessions[tenantId] = loaded;
                return loaded;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load sandbox session {TenantId} from DbContext", tenantId);
        }

        return new SandboxSessionConfig(
            tenantId,
            IsSandboxMode: true,
            SimulatedLatencyMs: 45,
            SimulateFailures: false,
            FailureRatePercent: 0.0,
            new List<string> { "X-SGrowing-Sandbox: true" }
        );
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
