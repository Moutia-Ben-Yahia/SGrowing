using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Dev.Gateway;
using Xunit;

namespace SGrowing.Tests.Dev;

public class DevGatewayTests
{
    [Fact]
    public void EvaluateRateLimit_WhenWithinCapacity_AllowsRequestAndDecrementsTokens()
    {
        var engine = new DevGatewayEngine();
        var request = new RateLimitCheckRequest(
            ApiKey: "key_test_123",
            TenantId: "tenant_alpha",
            Tier: DeveloperTier.Growth,
            RequestedTokens: 5
        );

        var result = engine.EvaluateRateLimit(request);

        Assert.True(result.IsAllowed);
        Assert.Equal(DeveloperTier.Growth, result.Tier);
        Assert.Equal(100, result.Capacity);
        Assert.Equal(95, result.RemainingTokens);
        Assert.Equal(0, result.RetryAfterSeconds);
    }

    [Fact]
    public void EvaluateRateLimit_WhenExceedingCapacity_RejectsWithRetryAfter()
    {
        var engine = new DevGatewayEngine();
        var request = new RateLimitCheckRequest(
            ApiKey: "key_test_exhaust",
            TenantId: "tenant_beta",
            Tier: DeveloperTier.Starter,
            RequestedTokens: 30 // Starter capacity is 25
        );

        var result = engine.EvaluateRateLimit(request);

        Assert.False(result.IsAllowed);
        Assert.True(result.RetryAfterSeconds >= 1);
        Assert.Equal(25, result.Capacity);
    }

    [Fact]
    public void GetTierQuota_ReturnsCorrectLimitsForEnterpriseAndStarter()
    {
        var engine = new DevGatewayEngine();

        var starter = engine.GetTierQuota(DeveloperTier.Starter);
        var enterprise = engine.GetTierQuota(DeveloperTier.Enterprise);
        var partner = engine.GetTierQuota(DeveloperTier.Partner);

        Assert.Equal(100, starter.RequestsPerMinute);
        Assert.Equal(25, starter.BurstCapacity);

        Assert.Equal(2500, enterprise.RequestsPerMinute);
        Assert.Equal(500, enterprise.BurstCapacity);

        Assert.Equal(10000, partner.RequestsPerMinute);
        Assert.Equal(2000, partner.BurstCapacity);
    }

    [Fact]
    public void ConfigureSandboxSession_StoresAndRetrievesSessionConfig()
    {
        var engine = new DevGatewayEngine();
        var config = new SandboxSessionConfig(
            TenantId: "tenant_gamma",
            IsSandboxMode: true,
            SimulatedLatencyMs: 80,
            SimulateFailures: false,
            FailureRatePercent: 0.0,
            MockHeaders: new List<string> { "X-Sandbox-Tenant: gamma" }
        );

        var configured = engine.ConfigureSandboxSession(config);
        var retrieved = engine.GetSandboxSession("tenant_gamma");

        Assert.NotNull(retrieved);
        Assert.Equal(80, retrieved.SimulatedLatencyMs);
        Assert.True(retrieved.IsSandboxMode);
    }

    [Fact]
    public void SimulateRequest_WhenCapiOrIdentity_ReturnsMockedSuccessPayload()
    {
        var engine = new DevGatewayEngine();
        var capiReq = new SandboxSimulationRequest(
            TenantId: "tenant_delta",
            Endpoint: "/api/v1/advertising/capi/events",
            HttpMethod: "POST",
            PayloadJson: "{\"event\":\"Purchase\"}"
        );

        var response = engine.SimulateRequest(capiReq);

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.IsSandbox);
        Assert.Contains("simulated_success", response.ResponsePayload);
        Assert.StartsWith("sbx_", response.TraceId);
    }

    [Fact]
    public void SimulateRequest_WhenForceError_Returns500SimulatedFailure()
    {
        var engine = new DevGatewayEngine();
        var errorReq = new SandboxSimulationRequest(
            TenantId: "tenant_epsilon",
            Endpoint: "/api/v1/customer-data/identity/stitch",
            HttpMethod: "POST",
            PayloadJson: "{}",
            ForceError: true
        );

        var response = engine.SimulateRequest(errorReq);

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("SimulatedSandboxFailure", response.ResponsePayload);
    }

    [Fact]
    public void ExportOpenApiSpec_PartitionsPathsByTargetRole()
    {
        var engine = new DevGatewayEngine();

        var publicSpec = engine.ExportOpenApiSpec(new OpenApiExportQuery(TargetRole: "public"));
        var adminSpec = engine.ExportOpenApiSpec(new OpenApiExportQuery(TargetRole: "admin"));

        Assert.Equal("public", publicSpec.TargetRole);
        Assert.Equal("admin", adminSpec.TargetRole);
        Assert.True(adminSpec.EndpointsCount > publicSpec.EndpointsCount);
        var paths = Assert.IsAssignableFrom<Dictionary<string, object>>(publicSpec.Spec["paths"]);
        Assert.True(paths.ContainsKey("/api/v1/advertising/capi/events"));
    }

    [Fact]
    public void SimulateRequest_WhenWebhookEndpoint_ReturnsSandboxSinkDelivery()
    {
        var engine = new DevGatewayEngine();
        var webhookReq = new SandboxSimulationRequest(
            TenantId: "tenant_webhook_test",
            Endpoint: "/api/v1/developer/webhooks/deliver",
            HttpMethod: "POST",
            PayloadJson: "{\"event\":\"order.created\"}"
        );

        var response = engine.SimulateRequest(webhookReq);

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.IsSandbox);
        Assert.Contains("delivered_to_sandbox_sink", response.ResponsePayload);
        Assert.Contains("sandbox-receiver.local", response.ResponsePayload);
    }

    [Fact]
    public void SimulateRequest_WhenUnknownEndpoint_ReturnsSandboxEchoPayload()
    {
        var engine = new DevGatewayEngine();
        var unknownReq = new SandboxSimulationRequest(
            TenantId: "tenant_custom_echo",
            Endpoint: "/api/v1/custom/reporting/export",
            HttpMethod: "GET",
            PayloadJson: null
        );

        var response = engine.SimulateRequest(unknownReq);

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.IsSandbox);
        Assert.Contains("sandbox_echo", response.ResponsePayload);
        Assert.Contains("/api/v1/custom/reporting/export", response.ResponsePayload);
        Assert.Contains("tenant_custom_echo", response.ResponsePayload);
    }

    [Fact]
    public void GetSandboxSession_ReturnsDefaultFallbackSession_WhenNotConfigured()
    {
        var engine = new DevGatewayEngine();

        var fallback = engine.GetSandboxSession("unconfigured_tenant_999");

        Assert.NotNull(fallback);
        Assert.True(fallback.IsSandboxMode);
        Assert.Equal(45, fallback.SimulatedLatencyMs);
        Assert.False(fallback.SimulateFailures);
    }

    [Fact]
    public void ConfigureSandboxSession_WithDurableStore_PreservesSessionAcrossEngineRestarts()
    {
        var store = new InMemoryDurableDevGatewayPersistenceStore();
        var engine1 = new DevGatewayEngine(store);

        var config = new SandboxSessionConfig(
            TenantId: "tenant_durable_sbx",
            IsSandboxMode: true,
            SimulatedLatencyMs: 150,
            SimulateFailures: true,
            FailureRatePercent: 75.0,
            MockHeaders: new List<string> { "X-Durable: true" }
        );

        engine1.ConfigureSandboxSession(config);

        // Simulate application restart with a fresh engine instance sharing the durable store
        var engine2 = new DevGatewayEngine(store);
        var restored = engine2.GetSandboxSession("tenant_durable_sbx");

        Assert.NotNull(restored);
        Assert.Equal(150, restored.SimulatedLatencyMs);
        Assert.True(restored.SimulateFailures);
        Assert.Equal(75.0, restored.FailureRatePercent);
        Assert.Contains("X-Durable: true", restored.MockHeaders);
    }

    [Fact]
    public void EvaluateRateLimit_WithDurableStore_PreservesTokenConsumptionAcrossEngineRestarts()
    {
        var store = new InMemoryDurableDevGatewayPersistenceStore();
        var engine1 = new DevGatewayEngine(store);

        var req = new RateLimitCheckRequest(
            ApiKey: "durable_key_001",
            TenantId: "tenant_ratelimit_durable",
            Tier: DeveloperTier.Starter,
            RequestedTokens: 20 // Starter has 25 burst capacity
        );

        var res1 = engine1.EvaluateRateLimit(req);
        Assert.True(res1.IsAllowed);
        Assert.Equal(5, res1.RemainingTokens);

        // Simulate application restart
        var engine2 = new DevGatewayEngine(store);

        // Request 20 tokens: restored bucket had only 5 remaining (out of 25 capacity), so requesting 20 must fail
        var req2 = new RateLimitCheckRequest(
            ApiKey: "durable_key_001",
            TenantId: "tenant_ratelimit_durable",
            Tier: DeveloperTier.Starter,
            RequestedTokens: 20
        );

        var res2 = engine2.EvaluateRateLimit(req2);
        Assert.False(res2.IsAllowed, "Rate limit state must be preserved across restarts to prevent quota evasion");
        Assert.True(res2.RemainingTokens < 20);
        Assert.True(res2.RetryAfterSeconds >= 1);
    }

    [Fact]
    public void ConfigureSandboxSession_WithDbContext_SavesEntityAndSurvivesEngineRestart()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using (var db1 = new SGrowingDbContext(options))
        {
            var engine1 = new DevGatewayEngine(db1);
            var config = new SandboxSessionConfig(
                TenantId: "tenant_ef_sandbox",
                IsSandboxMode: true,
                SimulatedLatencyMs: 95,
                SimulateFailures: false,
                FailureRatePercent: 0.0,
                MockHeaders: new List<string> { "X-EF-Persistence: active" }
            );

            engine1.ConfigureSandboxSession(config);

            // Verify directly in DbContext
            var entity = Assert.Single(db1.DevSandboxSessions);
            Assert.Equal("tenant_ef_sandbox", entity.TenantId);
            Assert.Equal(95, entity.SimulatedLatencyMs);
        }

        // Restart with fresh DbContext and fresh engine
        using (var db2 = new SGrowingDbContext(options))
        {
            var engine2 = new DevGatewayEngine(db2);
            var restored = engine2.GetSandboxSession("tenant_ef_sandbox");

            Assert.NotNull(restored);
            Assert.Equal(95, restored.SimulatedLatencyMs);
            Assert.Contains("X-EF-Persistence: active", restored.MockHeaders);
        }
    }

    [Fact]
    public void EvaluateRateLimit_WithDbContext_SavesRateLimitBucketAndSurvivesEngineRestart()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using (var db1 = new SGrowingDbContext(options))
        {
            var engine1 = new DevGatewayEngine(db1);
            var req = new RateLimitCheckRequest(
                ApiKey: "api_key_ef_db",
                TenantId: "tenant_ef_rate",
                Tier: DeveloperTier.Growth, // 100 burst capacity
                RequestedTokens: 90
            );

            var res1 = engine1.EvaluateRateLimit(req);
            Assert.True(res1.IsAllowed);
            Assert.Equal(10, res1.RemainingTokens);

            var bucket = Assert.Single(db1.DevRateLimitBuckets);
            Assert.Equal("tenant_ef_rate:api_key_ef_db", bucket.BucketKey);
        }

        using (var db2 = new SGrowingDbContext(options))
        {
            var engine2 = new DevGatewayEngine(db2);
            var reqExceed = new RateLimitCheckRequest(
                ApiKey: "api_key_ef_db",
                TenantId: "tenant_ef_rate",
                Tier: DeveloperTier.Growth,
                RequestedTokens: 90 // Fresh bucket has 100 capacity. Restored bucket has consumed tokens, so requesting 90 must fail!
            );

            var res2 = engine2.EvaluateRateLimit(reqExceed);
            Assert.False(res2.IsAllowed, "Restored bucket has consumed tokens and must reject large request");
            Assert.True(res2.RemainingTokens < 90);
        }
    }
}
