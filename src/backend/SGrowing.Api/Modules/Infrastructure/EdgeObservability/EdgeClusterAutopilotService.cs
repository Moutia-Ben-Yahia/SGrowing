namespace SGrowing.Api.Modules.Infrastructure.EdgeObservability;

/// <summary>
/// Contract for the autonomous edge cluster autopilot service orchestrating
/// node health monitoring, self-healing pod evacuation, and partition tolerance.
/// </summary>
public interface IEdgeClusterAutopilotService
{
    /// <summary>Registers or updates node telemetry and evaluates state changes.</summary>
    EdgeNodeTelemetry RegisterOrUpdateNode(EdgeNodeTelemetry telemetry);

    /// <summary>Retrieves all registered edge compute nodes.</summary>
    IReadOnlyList<EdgeNodeTelemetry> GetAllNodes();

    /// <summary>Retrieves a single edge compute node by its identifier.</summary>
    EdgeNodeTelemetry? GetNode(string nodeId);

    /// <summary>Retrieves cluster-wide health status and recent evacuation records.</summary>
    ClusterHealthResponse GetClusterHealth(string? clusterId = null);

    /// <summary>Manually cordons and drains an edge node, evacuating active workloads.</summary>
    DrainNodeResponse DrainNode(string nodeId, string? reason = null, bool force = false);

    /// <summary>Evaluates a node for degradation and executes an autonomous evacuation plan if necessary.</summary>
    PodEvacuationPlan? EvaluateAndTriggerEvacuationIfDegraded(string nodeId);

    /// <summary>Retrieves all generated pod evacuation plans.</summary>
    IReadOnlyList<PodEvacuationPlan> GetEvacuationPlans();

    /// <summary>Sets the edge cluster's WAN backhaul connectivity status.</summary>
    void SetWanConnectivity(string clusterId, bool isConnected);

    /// <summary>Checks whether the edge cluster's WAN backhaul is currently connected.</summary>
    bool IsWanConnected(string clusterId);

    /// <summary>Indicates whether localized autonomous partition-tolerant mode is active.</summary>
    bool IsPartitionTolerantModeActive(string clusterId);

    /// <summary>Prunes or flags stale edge nodes whose heartbeat has exceeded the threshold.</summary>
    void PruneStaleNodes(TimeSpan heartbeatTimeout, DateTimeOffset? referenceTime = null);
}

/// <summary>
/// Autonomous Kubernetes/Nomad edge cluster autopilot service.
/// Provides continuous health monitoring, autonomous pod evacuation, and partition tolerance.
/// </summary>
public class EdgeClusterAutopilotService : IEdgeClusterAutopilotService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, EdgeNodeTelemetry> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PodEvacuationPlan> _evacuationPlans = [];
    private readonly Dictionary<string, bool> _wanConnectivity = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Default cluster identifier if none is specified.
    /// </summary>
    public const string DefaultClusterId = "edge-cluster-primary";

    /// <summary>
    /// Thermal limit in Celsius triggering thermal degradation.
    /// </summary>
    public const double ThermalThresholdCelsius = 80.0;

    /// <summary>
    /// Kernel packet loss percentage triggering network flap degradation.
    /// </summary>
    public const double PacketLossThresholdPct = 3.0;

    /// <summary>
    /// Socket flap count triggering network flap degradation.
    /// </summary>
    public const long SocketFlapThreshold = 5;

    public EdgeClusterAutopilotService()
    {
        // Default cluster starts with WAN connected
        _wanConnectivity[DefaultClusterId] = true;
    }

    public EdgeNodeTelemetry RegisterOrUpdateNode(EdgeNodeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        PodEvacuationPlan? triggeredPlan = null;
        EdgeNodeTelemetry resultingTelemetry;

        lock (_lock)
        {
            var clusterId = string.IsNullOrWhiteSpace(telemetry.ClusterId) ? DefaultClusterId : telemetry.ClusterId;
            if (!_wanConnectivity.ContainsKey(clusterId))
            {
                _wanConnectivity[clusterId] = telemetry.IsWanConnected;
            }

            // Determine evaluated health state
            var evaluatedState = telemetry.State;

            // Do not override if already evacuating or drained
            if (telemetry.State != NodeHealthState.NodeEvacuating && telemetry.State != NodeHealthState.DrainedOffline)
            {
                if (telemetry.TemperatureCelsius > ThermalThresholdCelsius)
                {
                    evaluatedState = NodeHealthState.DegradedThermal;
                }
                else if (telemetry.PacketLossPct > PacketLossThresholdPct || telemetry.SocketFlapCount >= SocketFlapThreshold)
                {
                    evaluatedState = NodeHealthState.DegradedNetworkFlap;
                }
                else
                {
                    evaluatedState = NodeHealthState.Healthy;
                }
            }

            resultingTelemetry = telemetry with
            {
                ClusterId = clusterId,
                State = evaluatedState
            };

            _nodes[resultingTelemetry.NodeId] = resultingTelemetry;

            // Trigger autonomous evacuation if degraded and has active pods
            if ((evaluatedState == NodeHealthState.DegradedThermal || evaluatedState == NodeHealthState.DegradedNetworkFlap)
                && resultingTelemetry.ActivePods.Count > 0)
            {
                triggeredPlan = ExecuteEvacuationInternal(resultingTelemetry.NodeId,
                    $"Autonomous self-healing triggered due to state {evaluatedState} (Temp: {telemetry.TemperatureCelsius}°C, Loss: {telemetry.PacketLossPct}%)");
                if (_nodes.TryGetValue(resultingTelemetry.NodeId, out var updatedNode))
                {
                    resultingTelemetry = updatedNode;
                }
            }
        }

        return resultingTelemetry;
    }

    public IReadOnlyList<EdgeNodeTelemetry> GetAllNodes()
    {
        lock (_lock)
        {
            return [.. _nodes.Values];
        }
    }

    public EdgeNodeTelemetry? GetNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        lock (_lock)
        {
            return _nodes.TryGetValue(nodeId, out var node) ? node : null;
        }
    }

    public ClusterHealthResponse GetClusterHealth(string? clusterId = null)
    {
        var targetClusterId = string.IsNullOrWhiteSpace(clusterId) ? DefaultClusterId : clusterId;

        lock (_lock)
        {
            var clusterNodes = _nodes.Values
                .Where(n => string.Equals(n.ClusterId, targetClusterId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var total = clusterNodes.Count;
            var healthy = clusterNodes.Count(n => n.State == NodeHealthState.Healthy);
            var degraded = clusterNodes.Count(n => n.State is NodeHealthState.DegradedThermal or NodeHealthState.DegradedNetworkFlap);
            var evacuating = clusterNodes.Count(n => n.State == NodeHealthState.NodeEvacuating);
            var offline = clusterNodes.Count(n => n.State == NodeHealthState.DrainedOffline);

            var wanConnected = _wanConnectivity.GetValueOrDefault(targetClusterId, true);
            var recentPlans = _evacuationPlans
                .OrderByDescending(p => p.InitiatedAtUtc)
                .Take(20)
                .ToList();

            return new ClusterHealthResponse(
                ClusterId: targetClusterId,
                WanBackhaulConnected: wanConnected,
                TotalNodes: total,
                HealthyNodes: healthy,
                DegradedNodes: degraded,
                EvacuatingNodes: evacuating,
                OfflineNodes: offline,
                Nodes: clusterNodes,
                RecentEvacuations: recentPlans);
        }
    }

    public DrainNodeResponse DrainNode(string nodeId, string? reason = null, bool force = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        lock (_lock)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
            {
                return new DrainNodeResponse(
                    Success: false,
                    NodeId: nodeId,
                    FinalState: NodeHealthState.DrainedOffline,
                    Plan: null,
                    Message: $"Node '{nodeId}' not found in cluster.");
            }

            var plan = ExecuteEvacuationInternal(nodeId, reason ?? "Manual operator drain initiated", force);

            var finalNode = _nodes[nodeId];
            return new DrainNodeResponse(
                Success: true,
                NodeId: nodeId,
                FinalState: finalNode.State,
                Plan: plan,
                Message: $"Node '{nodeId}' successfully drained and cordoned.");
        }
    }

    public PodEvacuationPlan? EvaluateAndTriggerEvacuationIfDegraded(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        lock (_lock)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
            {
                return null;
            }

            if (node.State is NodeHealthState.DegradedThermal or NodeHealthState.DegradedNetworkFlap)
            {
                return ExecuteEvacuationInternal(nodeId, $"Autonomous evacuation triggered for degraded node in state {node.State}");
            }

            return null;
        }
    }

    public IReadOnlyList<PodEvacuationPlan> GetEvacuationPlans()
    {
        lock (_lock)
        {
            return [.. _evacuationPlans];
        }
    }

    public void SetWanConnectivity(string clusterId, bool isConnected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);

        lock (_lock)
        {
            _wanConnectivity[clusterId] = isConnected;

            // Also update all nodes belonging to this cluster
            foreach (var (key, node) in _nodes.ToList())
            {
                if (string.Equals(node.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase))
                {
                    _nodes[key] = node with { IsWanConnected = isConnected };
                }
            }
        }
    }

    public bool IsWanConnected(string clusterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);

        lock (_lock)
        {
            return _wanConnectivity.GetValueOrDefault(clusterId, true);
        }
    }

    public bool IsPartitionTolerantModeActive(string clusterId)
    {
        return !IsWanConnected(clusterId);
    }

    public void PruneStaleNodes(TimeSpan heartbeatTimeout, DateTimeOffset? referenceTime = null)
    {
        var now = referenceTime ?? DateTimeOffset.UtcNow;

        lock (_lock)
        {
            foreach (var (id, node) in _nodes.ToList())
            {
                if (node.State == NodeHealthState.DrainedOffline)
                {
                    continue;
                }

                if (now - node.LastHeartbeatUtc > heartbeatTimeout)
                {
                    // Stale heartbeat detected: mark as drained offline and evacuate critical tasks
                    ExecuteEvacuationInternal(id, $"Node heartbeat timed out (last seen {node.LastHeartbeatUtc:O})");
                }
            }
        }
    }

    /// <summary>
    /// Executes pod evacuation under the lock.
    /// Finds healthy standby nodes, migrates critical Tier 1 pods, cleanly drains non-critical pods,
    /// and marks the node as DrainedOffline.
    /// </summary>
    private PodEvacuationPlan ExecuteEvacuationInternal(string sourceNodeId, string reason, bool force = false)
    {
        var sourceNode = _nodes[sourceNodeId];
        var initiatedAt = DateTimeOffset.UtcNow;

        // Temporarily mark as evacuating
        _nodes[sourceNodeId] = sourceNode with { State = NodeHealthState.NodeEvacuating };

        // Identify available healthy standby nodes in the same cluster
        var standbyCandidates = _nodes.Values
            .Where(n => !string.Equals(n.NodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(n.ClusterId, sourceNode.ClusterId, StringComparison.OrdinalIgnoreCase)
                     && n.State == NodeHealthState.Healthy
                     && n.CpuUtilizationPct < 85.0)
            .OrderBy(n => n.CpuUtilizationPct)
            .ThenBy(n => n.MemoryUtilizationPct)
            .ToList();

        var evacuatedDetails = new List<EvacuatedPodDetail>();

        var standbyIndex = 0;
        foreach (var pod in sourceNode.ActivePods)
        {
            if (pod.Tier == LoadSheddingTier.Tier1CriticalRoboticsNeverShed || pod.IsCritical)
            {
                // Tier 1 MUST be migrated to a standby edge node
                if (standbyCandidates.Count > 0)
                {
                    var targetNode = standbyCandidates[standbyIndex % standbyCandidates.Count];
                    standbyIndex++;

                    // Assign pod to target node
                    var updatedTargetPods = new List<EdgeWorkloadPod>(targetNode.ActivePods) { pod };
                    _nodes[targetNode.NodeId] = targetNode with
                    {
                        ActivePods = updatedTargetPods,
                        CpuUtilizationPct = Math.Min(100.0, targetNode.CpuUtilizationPct + (pod.RequiredCpuCores * 5.0))
                    };

                    evacuatedDetails.Add(new EvacuatedPodDetail(
                        PodId: pod.PodId,
                        PodName: pod.Name,
                        Tier: pod.Tier,
                        TargetNodeId: targetNode.NodeId,
                        ActionTaken: "MigratedToStandby",
                        Success: true));
                }
                else
                {
                    // No healthy standby node: emergency fallback to least loaded node or queued
                    var emergencyNode = _nodes.Values
                        .Where(n => !string.Equals(n.NodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(n => n.CpuUtilizationPct)
                        .FirstOrDefault();

                    if (emergencyNode is not null)
                    {
                        var updatedPods = new List<EdgeWorkloadPod>(emergencyNode.ActivePods) { pod };
                        _nodes[emergencyNode.NodeId] = emergencyNode with { ActivePods = updatedPods };

                        evacuatedDetails.Add(new EvacuatedPodDetail(
                            PodId: pod.PodId,
                            PodName: pod.Name,
                            Tier: pod.Tier,
                            TargetNodeId: emergencyNode.NodeId,
                            ActionTaken: "EmergencyMigration",
                            Success: true));
                    }
                    else
                    {
                        evacuatedDetails.Add(new EvacuatedPodDetail(
                            PodId: pod.PodId,
                            PodName: pod.Name,
                            Tier: pod.Tier,
                            TargetNodeId: null,
                            ActionTaken: "MigrationSuspendedNoStandby",
                            Success: false));
                    }
                }
            }
            else
            {
                // Non-critical workloads: cleanly drain (GracefullyTerminated / Suspended)
                var action = pod.Tier switch
                {
                    LoadSheddingTier.Tier2CoreCheckout when standbyCandidates.Count > 0 => "MigratedToStandby",
                    LoadSheddingTier.Tier2CoreCheckout => "GracefullyTerminated",
                    LoadSheddingTier.Tier3AnalyticsTelemetry => "Suspended",
                    LoadSheddingTier.Tier4BackgroundBatch => "GracefullyTerminated",
                    _ => "GracefullyTerminated"
                };

                string? targetId = null;
                if (action == "MigratedToStandby" && standbyCandidates.Count > 0)
                {
                    var targetNode = standbyCandidates[standbyIndex % standbyCandidates.Count];
                    standbyIndex++;
                    var updatedPods = new List<EdgeWorkloadPod>(targetNode.ActivePods) { pod };
                    _nodes[targetNode.NodeId] = targetNode with { ActivePods = updatedPods };
                    targetId = targetNode.NodeId;
                }

                evacuatedDetails.Add(new EvacuatedPodDetail(
                    PodId: pod.PodId,
                    PodName: pod.Name,
                    Tier: pod.Tier,
                    TargetNodeId: targetId,
                    ActionTaken: action,
                    Success: true));
            }
        }

        var completedAt = DateTimeOffset.UtcNow;
        var plan = new PodEvacuationPlan(
            PlanId: $"evac-{Guid.NewGuid():N}"[..12],
            SourceNodeId: sourceNodeId,
            Reason: reason,
            InitiatedAtUtc: initiatedAt,
            CompletedAtUtc: completedAt,
            EvacuatedPods: evacuatedDetails,
            Status: "Completed");

        _evacuationPlans.Add(plan);

        // Update source node to DrainedOffline and empty active pods
        _nodes[sourceNodeId] = sourceNode with
        {
            State = NodeHealthState.DrainedOffline,
            ActivePods = []
        };

        return plan;
    }
}
