using System.Globalization;

namespace SGrowing.Api.Modules.Security.GraphFraud;

public interface IGraphClusterAnalyzerService
{
    IReadOnlyList<IReadOnlySet<string>> DiscoverConnectedComponents();
    IReadOnlyList<FraudClusterRecord> DetectFraudClusters(double minRiskScore = 0.0, int limit = 50);
    FraudClusterRecord? GetClusterById(string clusterId);
    IReadOnlyList<CentralityMetric> CalculateCentralityMetrics();
    CentralityMetric? GetNodeCentrality(string nodeId);
    IReadOnlyList<string> DetectMuleNodes(int minConnectedCustomers = 3);
    double CalculateModularity(IReadOnlyList<IReadOnlySet<string>> communities);
}

public class GraphClusterAnalyzerService : IGraphClusterAnalyzerService
{
    private readonly IIdentityGraphEngine _engine;
    private readonly Dictionary<string, FraudClusterRecord> _cachedClusters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _analyzerLock = new();

    public GraphClusterAnalyzerService(IIdentityGraphEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public IReadOnlyList<IReadOnlySet<string>> DiscoverConnectedComponents()
    {
        var allNodes = _engine.GetAllNodes();
        if (allNodes.Count == 0) return [];

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new List<IReadOnlySet<string>>();

        foreach (var node in allNodes)
        {
            if (visited.Contains(node.NodeId)) continue;

            var component = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(node.NodeId);
            visited.Add(node.NodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                var outEdges = _engine.GetOutEdges(current);
                foreach (var edge in outEdges)
                {
                    if (visited.Add(edge.TargetNodeId))
                    {
                        queue.Enqueue(edge.TargetNodeId);
                    }
                }

                var inEdges = _engine.GetInEdges(current);
                foreach (var edge in inEdges)
                {
                    if (visited.Add(edge.SourceNodeId))
                    {
                        queue.Enqueue(edge.SourceNodeId);
                    }
                }
            }

            components.Add(component);
        }

        return components;
    }

    public double CalculateModularity(IReadOnlyList<IReadOnlySet<string>> communities)
    {
        if (communities.Count == 0) return 0.0;

        var allEdges = _engine.GetAllEdges();
        if (allEdges.Count == 0) return 0.0;

        // Total weight m of undirected edges
        double totalWeightM = 0.0;
        var nodeDegrees = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in allEdges)
        {
            totalWeightM += edge.Weight;
            nodeDegrees[edge.SourceNodeId] = nodeDegrees.GetValueOrDefault(edge.SourceNodeId) + edge.Weight;
            nodeDegrees[edge.TargetNodeId] = nodeDegrees.GetValueOrDefault(edge.TargetNodeId) + edge.Weight;
        }

        if (totalWeightM <= 0.0) return 0.0;

        double twoM = 2.0 * totalWeightM;
        double modularity = 0.0;

        // Map node to community index
        var nodeToComm = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = 0; c < communities.Count; c++)
        {
            foreach (var nid in communities[c])
            {
                nodeToComm[nid] = c;
            }
        }

        // Sum for each community
        for (var c = 0; c < communities.Count; c++)
        {
            var commNodes = communities[c];
            double internalWeight = 0.0;
            double totalDegree = 0.0;

            foreach (var nid in commNodes)
            {
                totalDegree += nodeDegrees.GetValueOrDefault(nid, 0.0);

                var outEdges = _engine.GetOutEdges(nid);
                foreach (var edge in outEdges)
                {
                    if (commNodes.Contains(edge.TargetNodeId))
                    {
                        internalWeight += edge.Weight;
                    }
                }
            }

            // internalWeight counts each internal directed edge once. In an undirected graph with M total directed edges,
            // modularity term = (internalWeight / totalWeightM) - (totalDegree / twoM)^2
            var term = (internalWeight / totalWeightM) - Math.Pow(totalDegree / twoM, 2.0);
            modularity += term;
        }

        return Math.Round(modularity, 4);
    }

    public IReadOnlyList<FraudClusterRecord> DetectFraudClusters(double minRiskScore = 0.0, int limit = 50)
    {
        var components = DiscoverConnectedComponents();
        var globalModularity = CalculateModularity(components);
        var centralityMetrics = CalculateCentralityMetrics().ToDictionary(m => m.NodeId, StringComparer.OrdinalIgnoreCase);

        var detectedClusters = new List<FraudClusterRecord>();

        lock (_analyzerLock)
        {
            for (var idx = 0; idx < components.Count; idx++)
            {
                var memberIds = components[idx];
                var clusterId = $"cluster_ring_{idx + 1:D4}";

                var (riskScore, riskFactors) = EvaluateClusterRisk(memberIds, centralityMetrics);

                var allNodesQuarantined = memberIds.Count > 0 && memberIds.All(nid =>
                {
                    var n = _engine.GetNode(nid);
                    return n != null && n.IsQuarantined;
                });

                var cluster = new FraudClusterRecord
                {
                    ClusterId = clusterId,
                    MemberNodeIds = memberIds,
                    ClusterRiskScore = Math.Round(riskScore, 2),
                    ModularityScore = globalModularity,
                    PrimaryRiskFactors = riskFactors,
                    DetectedAtUtc = DateTime.UtcNow,
                    IsContained = allNodesQuarantined
                };

                _cachedClusters[clusterId] = cluster;

                if (cluster.ClusterRiskScore >= minRiskScore)
                {
                    detectedClusters.Add(cluster);
                }
            }

            return detectedClusters
                .OrderByDescending(c => c.ClusterRiskScore)
                .Take(limit)
                .ToList();
        }
    }

    public FraudClusterRecord? GetClusterById(string clusterId)
    {
        if (string.IsNullOrWhiteSpace(clusterId)) return null;

        lock (_analyzerLock)
        {
            if (_cachedClusters.TryGetValue(clusterId, out var cached))
            {
                return cached;
            }

            // Run detection to populate cache
            DetectFraudClusters();
            return _cachedClusters.GetValueOrDefault(clusterId);
        }
    }

    public IReadOnlyList<CentralityMetric> CalculateCentralityMetrics()
    {
        var allNodes = _engine.GetAllNodes();
        if (allNodes.Count == 0) return [];

        var nodeMap = allNodes.ToDictionary(n => n.NodeId, StringComparer.OrdinalIgnoreCase);
        var totalNodes = allNodes.Count;

        // 1. Calculate degrees & customer connectivity
        var metrics = new Dictionary<string, CentralityMetric>(StringComparer.OrdinalIgnoreCase);
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in allNodes)
        {
            adjacency[node.NodeId] = [];
        }

        foreach (var node in allNodes)
        {
            var outEdges = _engine.GetOutEdges(node.NodeId);
            var inEdges = _engine.GetInEdges(node.NodeId);

            var neighborIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connectedCustomerCount = 0;

            foreach (var e in outEdges)
            {
                neighborIds.Add(e.TargetNodeId);
                adjacency[node.NodeId].Add(e.TargetNodeId);
                adjacency[e.TargetNodeId].Add(node.NodeId);
            }

            foreach (var e in inEdges)
            {
                neighborIds.Add(e.SourceNodeId);
                adjacency[node.NodeId].Add(e.SourceNodeId);
                adjacency[e.SourceNodeId].Add(node.NodeId);
            }

            foreach (var nId in neighborIds)
            {
                if (nodeMap.TryGetValue(nId, out var neighborNode) &&
                    neighborNode.NodeType == GraphNodeType.Customer)
                {
                    connectedCustomerCount++;
                }
            }

            var isMuleOrBridge = (node.NodeType != GraphNodeType.Customer && connectedCustomerCount >= 3) ||
                                 (node.NodeType == GraphNodeType.Customer && neighborIds.Count >= 10);

            metrics[node.NodeId] = new CentralityMetric
            {
                NodeId = node.NodeId,
                NodeType = node.NodeType,
                Degree = neighborIds.Count,
                PageRank = 1.0 / totalNodes,
                IsMuleOrBridge = isMuleOrBridge,
                LinkedCustomersCount = connectedCustomerCount
            };
        }

        // 2. PageRank iterative calculation (Power Iteration, damping = 0.85, 20 iterations)
        const double d = 0.85;
        var pr = allNodes.ToDictionary(n => n.NodeId, _ => 1.0 / totalNodes, StringComparer.OrdinalIgnoreCase);

        for (var iter = 0; iter < 20; iter++)
        {
            var newPr = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            double sinkContribution = 0.0;

            foreach (var node in allNodes)
            {
                var neighbors = adjacency[node.NodeId];
                if (neighbors.Count == 0)
                {
                    sinkContribution += pr[node.NodeId];
                }
            }

            foreach (var node in allNodes)
            {
                var neighbors = adjacency[node.NodeId];
                double incomingFlow = 0.0;

                foreach (var neighborId in neighbors)
                {
                    var neighborDegree = adjacency[neighborId].Count;
                    if (neighborDegree > 0)
                    {
                        incomingFlow += pr[neighborId] / neighborDegree;
                    }
                }

                newPr[node.NodeId] = ((1.0 - d) / totalNodes) + (d * (incomingFlow + (sinkContribution / totalNodes)));
            }

            pr = newPr;
        }

        // 3. Assemble combined metrics
        var result = new List<CentralityMetric>();
        foreach (var (nodeId, metric) in metrics)
        {
            result.Add(metric with
            {
                PageRank = Math.Round(pr.GetValueOrDefault(nodeId, 1.0 / totalNodes), 6)
            });
        }

        return result;
    }

    public CentralityMetric? GetNodeCentrality(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;

        var all = CalculateCentralityMetrics();
        return all.FirstOrDefault(m => string.Equals(m.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> DetectMuleNodes(int minConnectedCustomers = 3)
    {
        var metrics = CalculateCentralityMetrics();
        return metrics
            .Where(m => m.NodeType != GraphNodeType.Customer && m.LinkedCustomersCount >= minConnectedCustomers)
            .OrderByDescending(m => m.LinkedCustomersCount)
            .Select(m => m.NodeId)
            .ToList();
    }

    private (double RiskScore, IReadOnlyList<string> RiskFactors) EvaluateClusterRisk(
        IReadOnlySet<string> memberIds,
        Dictionary<string, CentralityMetric> centralityMetrics)
    {
        var factors = new List<string>();
        double score = 0.0;

        var memberNodes = memberIds
            .Select(_engine.GetNode)
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();

        var customerNodes = memberNodes.Where(n => n.NodeType == GraphNodeType.Customer).ToList();
        var deviceNodes = memberNodes.Where(n => n.NodeType == GraphNodeType.DeviceFingerprint).ToList();
        var bankRibNodes = memberNodes.Where(n => n.NodeType == GraphNodeType.BankRib).ToList();
        var phoneNodes = memberNodes.Where(n => n.NodeType == GraphNodeType.PhoneNumber).ToList();
        var addressNodes = memberNodes.Where(n => n.NodeType == GraphNodeType.PhysicalAddress).ToList();

        var customerCount = customerNodes.Count;

        // If single customer or isolated profile without collusion
        if (customerCount <= 1)
        {
            var singleCustRisk = customerNodes.Count == 1 ? customerNodes[0].RiskScore : 10.0;
            return (Math.Min(25.0, singleCustRisk), ["LOW_RISK_ISOLATED_IDENTITY"]);
        }

        // 1. Base identity multiplexing score
        if (customerCount >= 2)
        {
            if (customerCount >= 10)
            {
                score += 50.0;
                factors.Add($"LARGE_SCALE_IDENTITY_RING: {customerCount} distinct customer accounts clustered together.");
            }
            else if (customerCount >= 5)
            {
                score += 35.0;
                factors.Add($"IDENTITY_MULTIPLEXING: {customerCount} distinct customer accounts in cluster.");
            }
            else
            {
                score += 20.0;
                factors.Add($"IDENTITY_SHARING: {customerCount} customer accounts sharing entities.");
            }
        }

        // 2. Shared Bank RIBs (Critical anomaly)
        foreach (var ribNode in bankRibNodes)
        {
            var inEdges = _engine.GetInEdges(ribNode.NodeId);
            var linkedCusts = inEdges.Where(e => e.RelationType == GraphRelationType.TransferredToBank).Count();
            if (linkedCusts >= 2)
            {
                score += (linkedCusts >= 3 ? 35.0 : 25.0);
                factors.Add($"CRITICAL_BANK_RIB_SHARING: Bank RIB {ribNode.NodeId} is shared across {linkedCusts} customer accounts.");
            }
        }

        // 3. Shared Devices (Bot / Emulator Mule)
        foreach (var devNode in deviceNodes)
        {
            var inEdges = _engine.GetInEdges(devNode.NodeId);
            var linkedCusts = inEdges.Where(e => e.RelationType == GraphRelationType.PlacedFromDevice).Count();
            if (linkedCusts >= 2)
            {
                score += (linkedCusts >= 5 ? 30.0 : 20.0);
                factors.Add($"DEVICE_FINGERPRINT_COLLUSION: Device {devNode.NodeId} multiplexes {linkedCusts} customer identities.");
            }
        }

        // 4. Shared Phone Numbers
        foreach (var phoneNode in phoneNodes)
        {
            var inEdges = _engine.GetInEdges(phoneNode.NodeId);
            var linkedCusts = inEdges.Where(e => e.RelationType == GraphRelationType.RegisteredWithPhone).Count();
            if (linkedCusts >= 2)
            {
                score += 15.0;
                factors.Add($"SHARED_PHONE_NUMBER: Phone {phoneNode.NodeId} utilized across {linkedCusts} customer profiles.");
            }
        }

        // 5. Shared Physical Addresses
        foreach (var addrNode in addressNodes)
        {
            var inEdges = _engine.GetInEdges(addrNode.NodeId);
            var linkedCusts = inEdges.Where(e => e.RelationType == GraphRelationType.DeliveredToAddress).Count();
            if (linkedCusts >= 3)
            {
                score += 15.0;
                factors.Add($"PHYSICAL_ADDRESS_CONVERGENCE: Address {addrNode.NodeId} links {linkedCusts} customer accounts.");
            }
        }

        // 6. Mule & Bridge Nodes
        foreach (var mId in memberIds)
        {
            if (centralityMetrics.TryGetValue(mId, out var metric) && metric.IsMuleOrBridge)
            {
                score += 15.0;
                factors.Add($"MULE_BRIDGE_TOPOLOGY: Node {metric.NodeId} acts as a high-degree identity bridge (linked to {metric.LinkedCustomersCount} customers).");
                break; // add once per cluster
            }
        }

        // 7. COD (Cash On Delivery) refusal rate anomalies
        double totalRefusals = 0;
        double totalCodOrders = 0;
        foreach (var cust in customerNodes)
        {
            if (cust.Properties.TryGetValue("cod_refusal_rate", out var rateStr) &&
                double.TryParse(rateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
            {
                totalRefusals += rate;
                totalCodOrders += 1.0;
            }
            else if (cust.Properties.TryGetValue("refused_orders", out var refStr) &&
                     cust.Properties.TryGetValue("total_orders", out var totStr) &&
                     double.TryParse(refStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var refused) &&
                     double.TryParse(totStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var total) &&
                     total > 0)
            {
                totalRefusals += (refused / total);
                totalCodOrders += 1.0;
            }
        }

        if (totalCodOrders > 0)
        {
            var avgCodRefusal = totalRefusals / totalCodOrders;
            if (avgCodRefusal >= 0.75)
            {
                score += 30.0;
                factors.Add($"HIGH_COD_REFUSAL_RATE: Cluster exhibits severe {avgCodRefusal:P0} COD delivery refusal rate.");
            }
            else if (avgCodRefusal >= 0.50)
            {
                score += 20.0;
                factors.Add($"ELEVATED_COD_REFUSAL_RATE: Cluster exhibits elevated {avgCodRefusal:P0} COD refusal rate.");
            }
        }

        // 8. Individual Node Risk Scores incorporation
        if (customerNodes.Count > 0)
        {
            var avgCustomerRisk = customerNodes.Average(c => c.RiskScore);
            score += (avgCustomerRisk * 0.20);
        }

        var clampedScore = Math.Clamp(score, 0.0, 100.0);

        if (factors.Count == 0)
        {
            factors.Add("BASELINE_CLUSTER_MONITORING");
        }

        return (clampedScore, factors);
    }
}
