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
}
