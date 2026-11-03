using SGrowing.Api.Modules.Infrastructure.Srv6Traffic;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class Srv6TrafficTests
{
    private readonly Srv6TrafficEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededPoliciesAndTelemetry()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalSrv6PoliciesConfigured >= 1);
        Assert.True(overview.TotalMeshThroughputGbps > 50.0);
        Assert.NotEmpty(overview.ActivePolicies);
        Assert.NotEmpty(overview.RecentTelemetry);
    }

    [Fact]
    public void CreatePolicy_UltraLowLatency_ConfiguresSrv6PolicyPath()
    {
        var sids = new List<string> { "fc00:100:1::1", "fc00:100:2::1" };
        var policy = _engine.CreatePolicy(
            "Asia-Pacific Direct GPU Transit",
            "headend-equinix-ty2",
            "tailend-aws-ap-southeast-1",
            "ULTRA_LOW_LATENCY",
            sids
        );

        Assert.NotNull(policy);
        Assert.StartsWith("SRV6-POL-", policy.PolicyId);
        Assert.Equal("ULTRA_LOW_LATENCY", policy.TrafficOptimizationProfile);
        Assert.Equal(64.0, policy.PathLatencyMs);
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void RecordTelemetry_HighThroughput_RecordsForwardingMetrics()
    {
        var tel = _engine.RecordTelemetry("SRV6-POL-001", pps: 2500000.0, throughputGbps: 32.5, lossRatio: 0.000005);

        Assert.NotNull(tel);
        Assert.StartsWith("SRV6-TEL-", tel.TelemetryId);
        Assert.Equal(32.5, tel.ThroughputGbps);
        Assert.Equal(0.000005, tel.PacketLossRatio);
    }
}
