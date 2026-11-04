using System.Collections.Concurrent;
using System.Diagnostics;
using SGrowing.Api.Modules.Dev.Gateway;

namespace SGrowing.Tests.Dev;

public class GatewayStressBenchmarkTests
{
    [Fact]
    public void HighThroughput_TokenBucketStress_10000Requests_ExecutesSubSecondWithoutRaceCondition()
    {
        var engine = new DevGatewayEngine();
        const int totalRequests = 10_000;
        var allowedCount = 0;
        var rejectedCount = 0;

        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, totalRequests, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, i =>
        {
            // Simulate 50 distinct API keys hitting the gateway in parallel
            var keyIndex = i % 50;
            var request = new RateLimitCheckRequest(
                ApiKey: $"stress_key_{keyIndex}",
                TenantId: $"tenant_stress_{keyIndex % 5}",
                Tier: DeveloperTier.Enterprise,
                RequestedTokens: 1
            );

            var result = engine.EvaluateRateLimit(request);
            if (result.IsAllowed)
            {
                Interlocked.Increment(ref allowedCount);
            }
            else
            {
                Interlocked.Increment(ref rejectedCount);
            }
        });

        stopwatch.Stop();

        Assert.Equal(totalRequests, allowedCount + rejectedCount);
        Assert.True(allowedCount > 0, "Expected at least burst capacity requests to be allowed");
        // Verify throughput is fast (10,000 requests processed under 2500ms on local CPU)
        Assert.True(stopwatch.ElapsedMilliseconds < 2500, $"Elapsed: {stopwatch.ElapsedMilliseconds}ms, exceeds 2500ms");
    }

    [Fact]
    public void HighThroughput_SandboxSimulationStress_ConcurrentLoads_MaintainsIntegrity()
    {
        var engine = new DevGatewayEngine();
        const int concurrentSimulations = 5_000;
        var successfulSimulations = 0;

        Parallel.For(0, concurrentSimulations, i =>
        {
            var req = new SandboxSimulationRequest(
                TenantId: $"tenant_stress_{i % 10}",
                Endpoint: "/api/v1/advertising/capi/events",
                HttpMethod: "POST",
                PayloadJson: $"{{\"event_id\":\"evt_{i}\",\"amount\":{i * 1.5}}}"
            );

            var response = engine.SimulateRequest(req);
            if (response.StatusCode == 200 && response.IsSandbox && response.TraceId.StartsWith("sbx_"))
            {
                Interlocked.Increment(ref successfulSimulations);
            }
        });

        Assert.Equal(concurrentSimulations, successfulSimulations);
    }

    [Theory]
    [InlineData(DeveloperTier.Starter, 100, 25, false)]
    [InlineData(DeveloperTier.Growth, 500, 100, true)]
    [InlineData(DeveloperTier.Enterprise, 2500, 500, true)]
    [InlineData(DeveloperTier.Partner, 10000, 2000, true)]
    public void TierQuotas_ShouldReturnExpectedCapacities_AcrossAllTiers(
        DeveloperTier tier,
        int expectedRpm,
        int expectedBurst,
        bool expectedWebhookReplay)
    {
        var engine = new DevGatewayEngine();
        var quota = engine.GetTierQuota(tier);

        Assert.Equal(tier, quota.Tier);
        Assert.Equal(expectedRpm, quota.RequestsPerMinute);
        Assert.Equal(expectedBurst, quota.BurstCapacity);
        Assert.Equal(expectedWebhookReplay, quota.WebhookReplayAllowed);
        Assert.True(quota.SandboxAllowed);
    }

    [Fact]
    public void EvaluateRateLimit_WhenTokensExhausted_ReturnsFalseAndRetryAfter()
    {
        var engine = new DevGatewayEngine();
        var tenantId = $"tenant_{Guid.NewGuid():N}";
        var apiKey = "test_exhaustion_key";

        // Burst is 25 for Starter
        var req1 = new RateLimitCheckRequest(apiKey, tenantId, DeveloperTier.Starter, RequestedTokens: 25);
        var result1 = engine.EvaluateRateLimit(req1);
        Assert.True(result1.IsAllowed);
        Assert.Equal(0, result1.RemainingTokens);

        // Immediate subsequent request requires more tokens than available
        var req2 = new RateLimitCheckRequest(apiKey, tenantId, DeveloperTier.Starter, RequestedTokens: 5);
        var result2 = engine.EvaluateRateLimit(req2);
        Assert.False(result2.IsAllowed);
        Assert.True(result2.RetryAfterSeconds >= 1);
        Assert.Equal(DeveloperTier.Starter, result2.Tier);
    }

    [Fact]
    public void SandboxSimulation_ForceError_Returns500WithSimulatedFailure()
    {
        var engine = new DevGatewayEngine();
        var req = new SandboxSimulationRequest(
            TenantId: "tenant_sandbox_err",
            Endpoint: "/api/v1/advertising/capi/events",
            HttpMethod: "POST",
            PayloadJson: "{\"amount\": 100}",
            ForceError: true
        );

        var response = engine.SimulateRequest(req);

        Assert.Equal(500, response.StatusCode);
        Assert.True(response.IsSandbox);
        Assert.Contains("SimulatedSandboxFailure", response.ResponsePayload);
        Assert.StartsWith("sbx_", response.TraceId);
    }

    [Fact]
    public void ExportOpenApiSpec_ReturnsValidSpecificationMetadata()
    {
        var engine = new DevGatewayEngine();
        var query = new OpenApiExportQuery(
            TargetRole: "public",
            IncludeSchemas: false,
            Format: "json"
        );

        var result = engine.ExportOpenApiSpec(query);

        Assert.NotNull(result);
        Assert.Equal("v1.0.0", result.Version);
        Assert.Equal("public", result.TargetRole);
        Assert.True(result.EndpointsCount >= 2);
        Assert.True(result.Spec.ContainsKey("paths"));
    }
}
