using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.K8sIsolationMesh;

public interface IK8sIsolationMeshEngine
{
    K8sIsolationOverview GetOverview();
    NamespaceOperationResult ProvisionNamespace(ProvisionNamespaceCommand command);
    NamespaceOperationResult QuarantineNamespace(QuarantineNamespaceCommand command);
    NamespaceOperationResult RestoreNamespace(string namespaceId);
}

public class K8sIsolationMeshEngine : IK8sIsolationMeshEngine
{
    private readonly ConcurrentDictionary<string, K8sTenantNamespace> _namespaces = new();

    public K8sIsolationMeshEngine()
    {
        SeedNamespaces();
    }

    private void SeedNamespaces()
    {
        var now = DateTime.UtcNow;

        var ns1 = new K8sTenantNamespace(
            NamespaceId: "ns-tenant-vertex",
            TenantId: "tenant_vertex_enterprise",
            Provider: CloudProvider.AwsEks,
            ClusterName: "eks-us-east-prod-01",
            Level: IsolationLevel.ZeroTrustMtlsEnforced,
            ActivePodsCount: 24,
            CpuQuotaCores: 64.0,
            MemoryQuotaGb: 256.0,
            IsQuarantined: false,
            CreatedAt: now.AddDays(-30)
        );

        var ns2 = new K8sTenantNamespace(
            NamespaceId: "ns-tenant-apex",
            TenantId: "tenant_apex_fintech",
            Provider: CloudProvider.GoogleGke,
            ClusterName: "gke-europe-west-prod-02",
            Level: IsolationLevel.ZeroTrustMtlsEnforced,
            ActivePodsCount: 18,
            CpuQuotaCores: 48.0,
            MemoryQuotaGb: 192.0,
            IsQuarantined: false,
            CreatedAt: now.AddDays(-15)
        );

        var ns3 = new K8sTenantNamespace(
            NamespaceId: "ns-tenant-sandbox-rogue",
            TenantId: "tenant_unverified_trial",
            Provider: CloudProvider.AzureAks,
            ClusterName: "aks-us-west-stage-01",
            Level: IsolationLevel.HardenedEbpfQuarantine,
            ActivePodsCount: 4,
            CpuQuotaCores: 8.0,
            MemoryQuotaGb: 32.0,
            IsQuarantined: true, // Quarantined for egress anomaly
            CreatedAt: now.AddHours(-18)
        );

        _namespaces[ns1.NamespaceId] = ns1;
        _namespaces[ns2.NamespaceId] = ns2;
        _namespaces[ns3.NamespaceId] = ns3;
    }

    public K8sIsolationOverview GetOverview()
    {
        var list = _namespaces.Values.OrderByDescending(n => n.CreatedAt).ToList();
        var quarantined = list.Count(n => n.IsQuarantined);
        var totalPods = list.Sum(n => n.ActivePodsCount);

        return new K8sIsolationOverview(
            TotalNamespacesCount: list.Count,
            QuarantinedCount: quarantined,
            TotalActivePods: totalPods,
            AverageClusterLoadPct: 38.5,
            Namespaces: list
        );
    }

    public NamespaceOperationResult ProvisionNamespace(ProvisionNamespaceCommand command)
    {
        var nsId = $"ns-tenant-{Guid.NewGuid().ToString("N")[..6].ToLowerInvariant()}";
        var ns = new K8sTenantNamespace(
            NamespaceId: nsId,
            TenantId: command.TenantId,
            Provider: command.Provider,
            ClusterName: command.ClusterName,
            Level: IsolationLevel.ZeroTrustMtlsEnforced,
            ActivePodsCount: 2,
            CpuQuotaCores: command.CpuCores,
            MemoryQuotaGb: command.MemoryGb,
            IsQuarantined: false,
            CreatedAt: DateTime.UtcNow
        );

        _namespaces[nsId] = ns;

        return new NamespaceOperationResult(
            NamespaceId: nsId,
            Status: "Provisioned",
            Message: $"Namespace {nsId} provisioned on {command.Provider} cluster {command.ClusterName} with strict eBPF mTLS and Vault secret injection."
        );
    }

    public NamespaceOperationResult QuarantineNamespace(QuarantineNamespaceCommand command)
    {
        if (!_namespaces.TryGetValue(command.NamespaceId, out var existing))
        {
            throw new KeyNotFoundException($"Namespace {command.NamespaceId} not found.");
        }

        var updated = existing with
        {
            IsQuarantined = true,
            Level = IsolationLevel.HardenedEbpfQuarantine
        };

        _namespaces[command.NamespaceId] = updated;

        return new NamespaceOperationResult(
            NamespaceId: command.NamespaceId,
            Status: "Quarantined",
            Message: $"eBPF network policy clamped: All egress/ingress traffic blocked on {command.NamespaceId}. Reason: {command.Reason}."
        );
    }

    public NamespaceOperationResult RestoreNamespace(string namespaceId)
    {
        if (!_namespaces.TryGetValue(namespaceId, out var existing))
        {
            throw new KeyNotFoundException($"Namespace {namespaceId} not found.");
        }

        var updated = existing with
        {
            IsQuarantined = false,
            Level = IsolationLevel.ZeroTrustMtlsEnforced
        };

        _namespaces[namespaceId] = updated;

        return new NamespaceOperationResult(
            NamespaceId: namespaceId,
            Status: "Restored",
            Message: $"Namespace {namespaceId} restored to ZeroTrustMtlsEnforced state with active pod communication."
        );
    }
}
