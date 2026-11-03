using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.K8sIsolationMesh;

public enum CloudProvider
{
    AwsEks = 0,
    GoogleGke = 1,
    AzureAks = 2
}

public enum IsolationLevel
{
    StandardNamespace = 0,
    HardenedEbpfQuarantine = 1,
    ZeroTrustMtlsEnforced = 2
}

public record K8sTenantNamespace(
    string NamespaceId,
    string TenantId,
    CloudProvider Provider,
    string ClusterName,
    IsolationLevel Level,
    int ActivePodsCount,
    double CpuQuotaCores,
    double MemoryQuotaGb,
    bool IsQuarantined,
    DateTime CreatedAt
);

public record K8sIsolationOverview(
    int TotalNamespacesCount,
    int QuarantinedCount,
    int TotalActivePods,
    double AverageClusterLoadPct,
    List<K8sTenantNamespace> Namespaces
);

public record ProvisionNamespaceCommand(
    string TenantId,
    CloudProvider Provider,
    string ClusterName,
    double CpuCores,
    double MemoryGb
);

public record QuarantineNamespaceCommand(
    string NamespaceId,
    string Reason
);

public record NamespaceOperationResult(
    string NamespaceId,
    string Status,
    string Message
);
