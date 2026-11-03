using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.BgpGeoSteering;

public interface IBgpGeoSteeringEngine
{
    BgpGeoSteeringOverview GetOverview();
    IngestProbeResult IngestProbe(IngestProbeCommand command);
    OverrideSteeringResult OverrideSteering(OverrideSteeringCommand command);
}

public class BgpGeoSteeringEngine : IBgpGeoSteeringEngine
{
    private readonly List<EdgePopNode> _pops =
    [
        new("POP-IAD-01", "Ashburn", "United States", "NorthAmerica", 13335, "198.51.100.1", 14, 1.2, 0.0, "Healthy"),
        new("POP-FRA-01", "Frankfurt", "Germany", "Europe", 13335, "198.51.100.2", 18, 1.5, 0.0, "Healthy"),
        new("POP-NRT-01", "Tokyo", "Japan", "AsiaPacific", 13335, "198.51.100.3", 26, 2.1, 0.0, "Healthy"),
        new("POP-GRU-01", "Sao Paulo", "Brazil", "LatinAmerica", 13335, "198.51.100.4", 78, 8.4, 0.8, "Degraded")
    ];

    private readonly List<GeoSteeringDnsPolicy> _policies =
    [
        new("POL-DNS-01", "api.sgrowing.io", "A", "LatencyOptimized", "POP-IAD-01", 0, "Active"),
        new("POL-DNS-02", "voice.sgrowing.io", "A", "GeoProximity", "POP-FRA-01", 0, "Active"),
        new("POL-DNS-03", "edge.sgrowing.io", "ALIAS", "FailoverSurge", "POP-NRT-01", 0, "Active")
    ];

    public BgpGeoSteeringOverview GetOverview()
    {
        var totalPops = _pops.Count;
        var healthyPops = _pops.Count(p => p.HealthStatus == "Healthy");
        var avgLatency = (int)_pops.Average(p => p.LatencyMs);
        var activeOverrides = _policies.Count(p => p.Status == "Overridden");

        return new BgpGeoSteeringOverview(
            totalPops,
            healthyPops,
            avgLatency,
            activeOverrides,
            _pops,
            _policies
        );
    }

    public IngestProbeResult IngestProbe(IngestProbeCommand command)
    {
        var probeReceipt = $"PROBE-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var isUnhealthy = command.PacketLossPct > 2.0 || command.LatencyMs > 150;
        var evaluatedHealth = isUnhealthy ? "Degraded" : "Healthy";
        var triggeredFailover = isUnhealthy;
        var remediation = isUnhealthy
            ? $"Shift DNS traffic away from {command.TargetPopId} to lowest latency peer POP"
            : "No routing adjustments required; latency/loss within SLA boundaries";

        return new IngestProbeResult(
            probeReceipt,
            command.TargetPopId,
            evaluatedHealth,
            triggeredFailover,
            remediation
        );
    }

    public OverrideSteeringResult OverrideSteering(OverrideSteeringCommand command)
    {
        var executionId = $"STEER-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var policy = _policies.FirstOrDefault(p => p.PolicyId == command.PolicyId) ?? _policies[0];
        var previousPop = policy.ActiveEndpointPopId;

        return new OverrideSteeringResult(
            executionId,
            command.PolicyId,
            previousPop,
            command.NewTargetPopId,
            DateTime.UtcNow,
            $"AUDIT-BGP-DNS-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}"
        );
    }
}
