namespace SGrowing.Api.Modules.Infrastructure.Srv6Traffic;

public enum Srv6SegmentType
{
    EndFunction,
    EndXCrossConnect,
    EndDx4DecapsulationIpv4,
    EndDx6DecapsulationIpv6,
    EndB6EncapsulationPolicy
}

public sealed record Srv6PolicyPath(
    string PolicyId,
    string PolicyName,
    string IngressHeadendNode,
    string EgressTailendNode,
    IReadOnlyList<string> SegmentListSids,
    string TrafficOptimizationProfile, // e.g., "ULTRA_LOW_LATENCY", "LEAST_COST"
    double PathLatencyMs,
    bool IsActive
);

public sealed record Srv6PacketForwardingTelemetry(
    string TelemetryId,
    string PolicyId,
    double PacketsPerSecond,
    double ThroughputGbps,
    double PacketLossRatio,
    DateTime MeasuredAtUtc
);

public sealed record Srv6TrafficOverview(
    int TotalSrv6PoliciesConfigured,
    int TotalActiveSrv6SegmentsCount,
    double AverageMultiCloudLatencyMs,
    double TotalMeshThroughputGbps,
    IReadOnlyList<Srv6PolicyPath> ActivePolicies,
    IReadOnlyList<Srv6PacketForwardingTelemetry> RecentTelemetry
);
