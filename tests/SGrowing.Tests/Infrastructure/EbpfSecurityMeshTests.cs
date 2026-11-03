using System;
using System.Linq;
using SGrowing.Api.Modules.Infrastructure.EbpfSecurityMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class EbpfSecurityMeshTests
{
    [Fact]
    public void GetOverview_ReturnsInitialProbesAndViolations()
    {
        var engine = new EbpfSecurityMeshEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveEbpfProbesCount >= 2);
        Assert.True(overview.TotalPacketsInspectedMillion > 0);
        Assert.True(overview.LateralThreatsBlockedCount >= 1);
        Assert.NotEmpty(overview.Probes);
        Assert.NotEmpty(overview.RecentViolations);
    }

    [Fact]
    public void BlockSuspiciousSocket_ProgramsKernelDropSuccessfully()
    {
        var engine = new EbpfSecurityMeshEngine();
        var request = new BlockSuspiciousSocketRequest(
            "untrusted-crawler-pod-88",
            "internal-vault-service",
            8200,
            "Anomalous lateral movement attempt without JWT identity attestation"
        );

        var alert = engine.BlockSuspiciousSocket(request);

        Assert.NotNull(alert);
        Assert.StartsWith("VIO-EBPF-", alert.AlertId);
        Assert.Equal("KERNEL_DROP", alert.EnforcementAction);
        Assert.Equal("untrusted-crawler-pod-88", alert.SourcePod);
        Assert.Equal("internal-vault-service", alert.TargetPod);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentViolations, v => v.AlertId == alert.AlertId);
    }

    [Fact]
    public void DeployProbe_RegistersNewKernelProbe()
    {
        var engine = new EbpfSecurityMeshEngine();
        var request = new DeployProbeRequest("k8s-node-worker-ap-southeast-1c", "6.8.0-1018-aws");

        var probe = engine.DeployProbe(request);

        Assert.NotNull(probe);
        Assert.StartsWith("EBPF-PRB-", probe.ProbeId);
        Assert.Equal("k8s-node-worker-ap-southeast-1c", probe.NodeName);
        Assert.Equal("PROBING_ACTIVE", probe.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Probes, p => p.ProbeId == probe.ProbeId);
    }
}
