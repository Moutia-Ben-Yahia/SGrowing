using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.PodSandboxingMesh;

public sealed class PodSandboxingMeshEngine
{
    private readonly ConcurrentDictionary<string, SandboxedPodRecord> _pods = new();
    private readonly ConcurrentDictionary<string, ChaosEvictionRecord> _evictions = new();
    private readonly ConcurrentBag<ChaosEvictionReceipt> _receiptsHistory = new();

    public PodSandboxingMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var p1 = new SandboxedPodRecord(
            "sbx-agent-scrape-4411",
            "sgrowing-sandbox",
            "gVisor (runsc kernel)",
            "AGT-SCRAPER-01",
            2048,
            2000,
            "k8s-node-worker-eu-central-1a",
            "RUNNING_ISOLATED",
            DateTime.UtcNow.AddHours(-4)
        );

        var p2 = new SandboxedPodRecord(
            "sbx-agent-coder-9922",
            "sgrowing-sandbox",
            "Kata Containers (microVM)",
            "AGT-CODER-02",
            4096,
            4000,
            "k8s-node-worker-eu-central-1b",
            "RUNNING_ISOLATED",
            DateTime.UtcNow.AddHours(-1)
        );

        _pods[p1.PodName] = p1;
        _pods[p2.PodName] = p2;

        var e1 = new ChaosEvictionRecord(
            "EVT-CHAOS-101",
            p1.PodName,
            "k8s-node-worker-eu-central-1a",
            "k8s-node-worker-eu-central-1c",
            340.0,
            "CHAOS_NODE_DRAIN_DRILL",
            DateTime.UtcNow.AddMinutes(-30)
        );

        _evictions[e1.EvictionId] = e1;
    }

    public PodSandboxingOverview GetOverview()
    {
        var podList = _pods.Values.ToList();
        var evictList = _evictions.Values.ToList();

        return new PodSandboxingOverview(
            podList.Count,
            1240,
            100.0,
            145.0,
            podList,
            evictList
        );
    }

    public SandboxPodProvisionResult ProvisionSandboxPod(ProvisionSandboxPodRequest request)
    {
        var podName = $"sbx-agent-{Guid.NewGuid():N}"[..18].ToLowerInvariant();
        var record = new SandboxedPodRecord(
            podName,
            "sgrowing-sandbox",
            request.RuntimeClass,
            request.AssignedAgentId,
            request.MemoryLimitMb,
            request.CpuLimitMillicores,
            "k8s-node-worker-us-east-1a",
            "RUNNING_ISOLATED",
            DateTime.UtcNow
        );

        _pods[podName] = record;

        return new SandboxPodProvisionResult(
            podName,
            "sgrowing-sandbox",
            request.RuntimeClass,
            "k8s-node-worker-us-east-1a",
            "RUNNING_ISOLATED",
            135
        );
    }

    public ChaosEvictionReceipt TriggerChaosEviction(TriggerChaosEvictionRequest request)
    {
        var pod = _pods.TryGetValue(request.PodName, out var p) ? p : _pods.Values.First();
        var targetNode = "k8s-node-worker-us-east-1b";

        var updated = pod with { NodeName = targetNode };
        _pods[pod.PodName] = updated;

        var receipt = new ChaosEvictionReceipt(
            $"RCP-EVCT-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            pod.PodName,
            pod.NodeName,
            targetNode,
            285.0,
            "RESCHEDULED_ZERO_LOSS_SUCCESS",
            DateTime.UtcNow
        );

        _receiptsHistory.Add(receipt);
        return receipt;
    }
}
