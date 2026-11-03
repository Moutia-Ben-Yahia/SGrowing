using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.EbpfSecurityMesh;

public sealed record EbpfProbeNodeRecord(
    string ProbeId,
    string NodeName,
    string KernelRelease,
    int HookedSyscallsCount,
    double InspectionLatencyMicroseconds,
    string Status // PROBING_ACTIVE, DEGRADED, RECOMPILING
);

public sealed record SocketViolationAlertRecord(
    string AlertId,
    string SourcePod,
    string SourceIp,
    string TargetPod,
    string TargetIp,
    int TargetPort,
    string Protocol, // TCP_HTTP, TCP_GRPC, RAW_SOCKET
    string EnforcementAction, // KERNEL_DROP, ALERT_ONLY, TLS_TERMINATE
    string AnomalyReason,
    DateTime DetectedAt
);

public sealed record EbpfMeshOverview(
    int ActiveEbpfProbesCount,
    int TotalPacketsInspectedMillion,
    int LateralThreatsBlockedCount,
    double MeanPacketLatencyMicroseconds,
    IReadOnlyList<EbpfProbeNodeRecord> Probes,
    IReadOnlyList<SocketViolationAlertRecord> RecentViolations
);

public sealed record BlockSuspiciousSocketRequest(
    string SourcePod,
    string TargetPod,
    int TargetPort,
    string AnomalyReason
);

public sealed record DeployProbeRequest(
    string NodeName,
    string KernelRelease
);
