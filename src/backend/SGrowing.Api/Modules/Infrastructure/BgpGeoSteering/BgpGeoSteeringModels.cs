using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.BgpGeoSteering;

public record EdgePopNode(
    string PopId,
    string City,
    string Country,
    string Region, // "NorthAmerica", "Europe", "AsiaPacific", "LatinAmerica"
    int BgpAsn,
    string AnycastIp,
    int LatencyMs,
    double JitterMs,
    double PacketLossPct,
    string HealthStatus // "Healthy", "Degraded", "DrainRouting", "Offline"
);

public record GeoSteeringDnsPolicy(
    string PolicyId,
    string DomainName,
    string RecordType, // "A", "AAAA", "ALIAS"
    string RoutingStrategy, // "LatencyOptimized", "GeoProximity", "FailoverSurge"
    string ActiveEndpointPopId,
    int FlapDampingBackoffSeconds,
    string Status // "Active", "Overridden", "Damped"
);

public record SyntheticProbeSample(
    string SampleId,
    string ProbeSourceRegion,
    string TargetPopId,
    int LatencyMs,
    double PacketLossPct,
    DateTime Timestamp
);

public record BgpGeoSteeringOverview(
    int TotalPopsCount,
    int HealthyPopsCount,
    int AverageGlobalLatencyMs,
    int ActiveSteeringOverridesCount,
    IReadOnlyList<EdgePopNode> Pops,
    IReadOnlyList<GeoSteeringDnsPolicy> Policies
);

public record IngestProbeCommand(
    string TargetPopId,
    string SourceRegion,
    int LatencyMs,
    double JitterMs,
    double PacketLossPct
);

public record IngestProbeResult(
    string ProbeReceiptId,
    string TargetPopId,
    string EvaluatedHealth,
    bool TriggeredDnsFailover,
    string RemediationAction
);

public record OverrideSteeringCommand(
    string PolicyId,
    string NewTargetPopId,
    string OverrideReason,
    string Operator
);

public record OverrideSteeringResult(
    string OverrideExecutionId,
    string PolicyId,
    string PreviousPopId,
    string ActivePopId,
    DateTime AppliedAt,
    string AuditReceipt
);
