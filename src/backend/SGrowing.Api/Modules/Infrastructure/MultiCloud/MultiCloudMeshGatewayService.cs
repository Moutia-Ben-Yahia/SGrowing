namespace SGrowing.Api.Modules.Infrastructure.MultiCloud;

/// <summary>
/// Thread-safe Anycast multi-cloud traffic router managing global cluster topology
/// and automatic failover routing across public and sovereign datacenters.
/// </summary>
public class MultiCloudMeshGatewayService : IMultiCloudMeshGatewayService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, CloudMeshNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private DisasterRecoveryDrillReport? _lastDrillReport;
    private long _currentTerm = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiCloudMeshGatewayService"/> class.
    /// By default seeds a 5-node sovereign mesh spanning AWS, Azure, GCP, Algiers, and Dubai.
    /// </summary>
    /// <param name="seedDefaultTopology">Whether to populate the initial sovereign multi-cloud mesh topology.</param>
    public MultiCloudMeshGatewayService(bool seedDefaultTopology = true)
    {
        if (seedDefaultTopology)
        {
            SeedInitialSovereignTopology();
        }
    }

    /// <inheritdoc />
    public void RegisterNode(CloudMeshNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.NodeId);

        lock (_lock)
        {
            // If the registered node is marked as write leader, demote any other existing write leader
            if (node.IsWriteLeader)
            {
                foreach (var (id, existingNode) in _nodes)
                {
                    if (existingNode.IsWriteLeader && !string.Equals(id, node.NodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        _nodes[id] = existingNode with { IsWriteLeader = false };
                    }
                }
            }

            _nodes[node.NodeId] = node;
        }
    }

    /// <inheritdoc />
    public bool UnregisterNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        lock (_lock)
        {
            return _nodes.Remove(nodeId);
        }
    }

    /// <inheritdoc />
    public void UpdateNodeHealth(string nodeId, RegionNodeStatus status, double latencyMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        lock (_lock)
        {
            if (!_nodes.TryGetValue(nodeId, out var existing))
            {
                return;
            }

            // Automatic < 5000ms latency detection:
            // Latency >= 5000ms triggers automatic degradation to protect ingress traffic
            var effectiveStatus = latencyMs >= 5000.0 && status == RegionNodeStatus.Healthy
                ? RegionNodeStatus.DegradedLatency
                : status;

            _nodes[nodeId] = existing with
            {
                HealthStatus = effectiveStatus,
                LatencyP99Ms = latencyMs
            };
        }
    }

    /// <inheritdoc />
    public CloudMeshNode? GetOptimalRoutingNode()
    {
        lock (_lock)
        {
            // Filter routable nodes: Healthy status and latency under 5000ms threshold
            var candidates = _nodes.Values
                .Where(n => n.HealthStatus == RegionNodeStatus.Healthy && n.LatencyP99Ms < 5000.0)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            // Order by weighted lowest latency: (Latency / Weight) ascending
            // Tie-break: lowest raw latency, highest weight, node ID
            return candidates
                .OrderBy(n => n.LatencyP99Ms / Math.Max(n.Weight, 0.0001))
                .ThenBy(n => n.LatencyP99Ms)
                .ThenByDescending(n => n.Weight)
                .ThenBy(n => n.NodeId, StringComparer.Ordinal)
                .First();
        }
    }

    /// <inheritdoc />
    public MultiCloudTopologyResponse GetClusterTopology()
    {
        lock (_lock)
        {
            var nodesList = _nodes.Values.ToList();
            int total = nodesList.Count;
            int healthy = nodesList.Count(n => n.HealthStatus == RegionNodeStatus.Healthy);
            double totalWeight = nodesList.Where(n => n.HealthStatus == RegionNodeStatus.Healthy).Sum(n => n.Weight);
            string? leaderId = nodesList.FirstOrDefault(n => n.IsWriteLeader)?.NodeId;

            return new MultiCloudTopologyResponse(
                TotalNodesCount: total,
                HealthyNodesCount: healthy,
                TotalCapacityWeight: Math.Round(totalWeight, 2),
                CurrentWriteLeaderId: leaderId,
                CurrentTerm: _currentTerm,
                Nodes: nodesList.AsReadOnly(),
                LastDrillReport: _lastDrillReport);
        }
    }

    /// <inheritdoc />
    public CloudMeshNode? GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        lock (_lock)
        {
            return _nodes.GetValueOrDefault(nodeId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CloudMeshNode> GetAllNodes()
    {
        lock (_lock)
        {
            return _nodes.Values.ToList();
        }
    }

    /// <inheritdoc />
    public bool SetWriteLeader(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        lock (_lock)
        {
            if (!_nodes.TryGetValue(nodeId, out var targetNode))
            {
                return false;
            }

            foreach (var (id, node) in _nodes)
            {
                bool isTarget = string.Equals(id, nodeId, StringComparison.OrdinalIgnoreCase);
                if (node.IsWriteLeader != isTarget)
                {
                    _nodes[id] = node with { IsWriteLeader = isTarget };
                }
            }

            return true;
        }
    }

    /// <inheritdoc />
    public void ClearWriteLeader()
    {
        lock (_lock)
        {
            foreach (var (id, node) in _nodes)
            {
                if (node.IsWriteLeader)
                {
                    _nodes[id] = node with { IsWriteLeader = false };
                }
            }
        }
    }

    /// <inheritdoc />
    public void SetLastDrillReport(DisasterRecoveryDrillReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        lock (_lock)
        {
            _lastDrillReport = report;
        }
    }

    /// <inheritdoc />
    public void SetCurrentTerm(long term)
    {
        lock (_lock)
        {
            if (term > _currentTerm)
            {
                _currentTerm = term;
            }
        }
    }

    private void SeedInitialSovereignTopology()
    {
        // 1. AWS Western Europe (Paris)
        _nodes["node-aws-eu-west-3"] = new CloudMeshNode(
            NodeId: "node-aws-eu-west-3",
            Provider: CloudProvider.Aws,
            RegionCode: "eu-west-3",
            Hostname: "aws-paris.sgrowing.cloud",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 18.5,
            Weight: 1.0,
            IsWriteLeader: false);

        // 2. Azure Western Europe (Amsterdam)
        _nodes["node-azure-westeurope"] = new CloudMeshNode(
            NodeId: "node-azure-westeurope",
            Provider: CloudProvider.Azure,
            RegionCode: "westeurope",
            Hostname: "azure-ams.sgrowing.cloud",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 21.0,
            Weight: 1.0,
            IsWriteLeader: false);

        // 3. GCP Europe West (Belgium)
        _nodes["node-gcp-europe-west1"] = new CloudMeshNode(
            NodeId: "node-gcp-europe-west1",
            Provider: CloudProvider.Gcp,
            RegionCode: "europe-west1",
            Hostname: "gcp-belgium.sgrowing.cloud",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 22.4,
            Weight: 1.0,
            IsWriteLeader: false);

        // 4. Sovereign Private Datacenter 1 (Algiers) - Default Primary Write Leader
        _nodes["node-sovereign-algiers-1"] = new CloudMeshNode(
            NodeId: "node-sovereign-algiers-1",
            Provider: CloudProvider.SovereignPrivateAlgiers,
            RegionCode: "dz-central-1",
            Hostname: "algiers1.sovereign.sgrowing.dz",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 12.0,
            Weight: 2.0,
            IsWriteLeader: true);

        // 5. Sovereign Private Datacenter 2 (Dubai)
        _nodes["node-sovereign-dubai-1"] = new CloudMeshNode(
            NodeId: "node-sovereign-dubai-1",
            Provider: CloudProvider.SovereignPrivateDubai,
            RegionCode: "me-central-1",
            Hostname: "dubai1.sovereign.sgrowing.ae",
            HealthStatus: RegionNodeStatus.Healthy,
            LatencyP99Ms: 28.0,
            Weight: 1.5,
            IsWriteLeader: false);
    }
}
