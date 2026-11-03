using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.K8sIsolationMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class K8sIsolationMeshTests
{
    private readonly K8sIsolationMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsMultiCloudNamespaces()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalNamespacesCount.Should().Be(3);
        overview.QuarantinedCount.Should().Be(1);
        overview.TotalActivePods.Should().BeGreaterThan(40);
        overview.Namespaces.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvisionNamespace_CreatesZeroTrustTenantNamespace()
    {
        var command = new ProvisionNamespaceCommand(
            TenantId: "tenant_quantum_analytics",
            Provider: CloudProvider.AwsEks,
            ClusterName: "eks-us-east-prod-02",
            CpuCores: 32.0,
            MemoryGb: 128.0
        );

        var result = _engine.ProvisionNamespace(command);

        result.Should().NotBeNull();
        result.Status.Should().Be("Provisioned");
        result.Message.Should().Contain("eBPF mTLS");
    }

    [Fact]
    public void QuarantineNamespace_AppliesEbpfQuarantine()
    {
        var command = new QuarantineNamespaceCommand("ns-tenant-vertex", "Outbound cryptomining port anomaly detected");
        var result = _engine.QuarantineNamespace(command);

        result.Should().NotBeNull();
        result.Status.Should().Be("Quarantined");
        result.Message.Should().Contain("eBPF network policy clamped");

        var overview = _engine.GetOverview();
        var ns = overview.Namespaces.Find(n => n.NamespaceId == "ns-tenant-vertex");
        ns!.IsQuarantined.Should().BeTrue();
        ns.Level.Should().Be(IsolationLevel.HardenedEbpfQuarantine);
    }

    [Fact]
    public void RestoreNamespace_RestoresZeroTrustOperation()
    {
        var result = _engine.RestoreNamespace("ns-tenant-sandbox-rogue");

        result.Should().NotBeNull();
        result.Status.Should().Be("Restored");

        var overview = _engine.GetOverview();
        var ns = overview.Namespaces.Find(n => n.NamespaceId == "ns-tenant-sandbox-rogue");
        ns!.IsQuarantined.Should().BeFalse();
        ns.Level.Should().Be(IsolationLevel.ZeroTrustMtlsEnforced);
    }
}
