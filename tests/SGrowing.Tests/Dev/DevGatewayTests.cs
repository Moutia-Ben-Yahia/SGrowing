using System;
using System.Collections.Generic;
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
}
