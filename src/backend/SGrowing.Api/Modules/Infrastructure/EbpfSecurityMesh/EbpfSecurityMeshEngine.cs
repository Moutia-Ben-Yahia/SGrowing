using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.EbpfSecurityMesh;

public sealed class EbpfSecurityMeshEngine
{
    private readonly ConcurrentDictionary<string, EbpfProbeNodeRecord> _probes = new();
    private readonly ConcurrentBag<SocketViolationAlertRecord> _violations = new();

    public EbpfSecurityMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var p1 = new EbpfProbeNodeRecord(
            "EBPF-PRB-EU-01",
            "k8s-node-worker-eu-central-1a",
            "6.8.0-1018-aws (Linux x86_64)",
            128,
            24.5,
            "PROBING_ACTIVE"
        );

        var p2 = new EbpfProbeNodeRecord(
            "EBPF-PRB-US-02",
            "k8s-node-worker-us-east-1b",
            "6.8.0-1018-aws (Linux x86_64)",
            128,
            26.2,
            "PROBING_ACTIVE"
        );

        _probes[p1.ProbeId] = p1;
        _probes[p2.ProbeId] = p2;

        var v1 = new SocketViolationAlertRecord(
            "VIO-EBPF-401",
            "sbx-agent-scrape-4411",
            "10.244.1.84",
            "core-postgres-primary",
            "10.244.0.12",
            5432,
            "TCP_POSTGRES",
            "KERNEL_DROP",
            "Untrusted sandboxed agent attempted direct raw TCP connect to primary DB without mTLS proxy",
            DateTime.UtcNow.AddMinutes(-12)
        );

        _violations.Add(v1);
    }

    public EbpfMeshOverview GetOverview()
    {
        var probeList = _probes.Values.ToList();
        var vioList = _violations.ToList();
        var blockedCount = vioList.Count(v => v.EnforcementAction == "KERNEL_DROP");
        var meanLatency = probeList.Count > 0 ? probeList.Average(p => p.InspectionLatencyMicroseconds) : 25.0;

        return new EbpfMeshOverview(
            probeList.Count,
            1420, // 1,420M packets inspected
            blockedCount,
            meanLatency,
            probeList,
            vioList
        );
    }

    public SocketViolationAlertRecord BlockSuspiciousSocket(BlockSuspiciousSocketRequest request)
    {
        var alertId = $"VIO-EBPF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var alert = new SocketViolationAlertRecord(
            alertId,
            request.SourcePod,
            "10.244.3.44",
            request.TargetPod,
            "10.244.0.89",
            request.TargetPort,
            "TCP_GRPC",
            "KERNEL_DROP",
            request.AnomalyReason,
            DateTime.UtcNow
        );

        _violations.Add(alert);
        return alert;
    }

    public EbpfProbeNodeRecord DeployProbe(DeployProbeRequest request)
    {
        var probeId = $"EBPF-PRB-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var probe = new EbpfProbeNodeRecord(
            probeId,
            request.NodeName,
            request.KernelRelease,
            128,
            23.8,
            "PROBING_ACTIVE"
        );

        _probes[probeId] = probe;
        return probe;
    }
}
