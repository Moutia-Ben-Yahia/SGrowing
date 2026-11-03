using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.Srv6Traffic;

public interface ISrv6TrafficEngine
{
    Srv6TrafficOverview GetOverview();
    Srv6PolicyPath CreatePolicy(string name, string ingress, string egress, string profile, List<string> sids);
    Srv6PacketForwardingTelemetry RecordTelemetry(string policyId, double pps, double throughputGbps, double lossRatio);
}

public sealed class Srv6TrafficEngine : ISrv6TrafficEngine
{
    private readonly ConcurrentDictionary<string, Srv6PolicyPath> _policies = new();
    private readonly ConcurrentDictionary<string, Srv6PacketForwardingTelemetry> _telemetry = new();

    public Srv6TrafficEngine()
    {
        SeedPolicies();
        SeedInitialTelemetry();
    }

    private void SeedPolicies()
    {
        var p1 = new Srv6PolicyPath(
            "SRV6-POL-001",
            "Transatlantic Low-Latency Edge Steering",
            "headend-aws-us-east-1",
            "tailend-gcp-europe-west-3",
            new List<string> { "fc00:100:1::1", "fc00:200:2::1", "fc00:300:3::1" },
            "ULTRA_LOW_LATENCY",
            68.4,
            true
        );
        _policies.TryAdd(p1.PolicyId, p1);
    }

    private void SeedInitialTelemetry()
    {
        var t1 = new Srv6PacketForwardingTelemetry(
            "SRV6-TEL-001",
            "SRV6-POL-001",
            PacketsPerSecond: 1420000.0,
            ThroughputGbps: 18.4,
            PacketLossRatio: 0.00001,
            DateTime.UtcNow.AddSeconds(-20)
        );
        _telemetry.TryAdd(t1.TelemetryId, t1);
    }

    public Srv6TrafficOverview GetOverview()
    {
        var policies = _policies.Values.ToList();
        var telemetry = _telemetry.Values.OrderByDescending(t => t.MeasuredAtUtc).Take(10).ToList();

        return new Srv6TrafficOverview(
            TotalSrv6PoliciesConfigured: policies.Count,
            TotalActiveSrv6SegmentsCount: 24,
            AverageMultiCloudLatencyMs: 68.4,
            TotalMeshThroughputGbps: 125.6,
            ActivePolicies: policies,
            RecentTelemetry: telemetry
        );
    }

    public Srv6PolicyPath CreatePolicy(string name, string ingress, string egress, string profile, List<string> sids)
    {
        double latency = profile == "ULTRA_LOW_LATENCY" ? 64.0 : 88.0;

        var policy = new Srv6PolicyPath(
            $"SRV6-POL-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            name,
            ingress,
            egress,
            sids,
            profile,
            latency,
            true
        );
        _policies.TryAdd(policy.PolicyId, policy);
        return policy;
    }

    public Srv6PacketForwardingTelemetry RecordTelemetry(string policyId, double pps, double throughputGbps, double lossRatio)
    {
        var tel = new Srv6PacketForwardingTelemetry(
            $"SRV6-TEL-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            policyId,
            pps,
            throughputGbps,
            lossRatio,
            DateTime.UtcNow
        );
        _telemetry.TryAdd(tel.TelemetryId, tel);
        return tel;
    }
}
