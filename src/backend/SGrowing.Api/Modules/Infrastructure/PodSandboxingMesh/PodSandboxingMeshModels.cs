using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.PodSandboxingMesh;

public sealed record PodSandboxingOverview(
    int ActiveIsolatedPodsCount,
    int TotalSandboxesProvisioned24h,
    double ZeroLossEvictionSuccessRatePct,
    double AverageContainerStartLatencyMs,
    IReadOnlyList<SandboxedPodRecord> ActivePods,
    IReadOnlyList<ChaosEvictionRecord> RecentEvictions
);

public sealed record SandboxedPodRecord(
    string PodName,
    string NamespaceName,
    string RuntimeClass, // gVisor (runsc), Kata Containers, Firecracker microVM
    string AssignedAgentId,
    int MemoryLimitMb,
    int CpuLimitMillicores,
    string NodeName,
    string Status,
    DateTime CreatedAt
);

public sealed record ChaosEvictionRecord(
    string EvictionId,
    string PodName,
    string SourceNode,
    string TargetNode,
    double ReschedulingLatencyMs,
    string EvictionReason,
    DateTime Timestamp
);

public sealed record ProvisionSandboxPodRequest(
    string AssignedAgentId,
    string RuntimeClass,
    int MemoryLimitMb,
    int CpuLimitMillicores,
    IReadOnlyList<string> SecurityCapabilitiesDrop
);

public sealed record SandboxPodProvisionResult(
    string PodName,
    string NamespaceName,
    string RuntimeClass,
    string AssignedNode,
    string Status,
    long ProvisionLatencyMs
);

public sealed record TriggerChaosEvictionRequest(
    string PodName,
    string ChaosScenario // NODE_OOM_SIMULATION, NETWORK_SPLIT_PARTITION, KERNEL_PANIC_FAKER
);

public sealed record ChaosEvictionReceipt(
    string EvictionId,
    string PodName,
    string SourceNode,
    string DestinationNode,
    double ZeroLossRescheduleTimeMs,
    string Status,
    DateTime Timestamp
);
