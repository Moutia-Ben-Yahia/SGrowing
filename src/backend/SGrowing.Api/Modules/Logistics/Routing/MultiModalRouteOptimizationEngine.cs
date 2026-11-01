using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Logistics.Routing;

/// <summary>
/// Multi-modal graph routing engine combining road freight, rail transport, and coastal shipping
/// across Algerian national and regional corridors with weather hazard avoidance.
/// </summary>
public sealed class MultiModalRouteOptimizationEngine
{
    private readonly DynamicWeatherHazardRadarService _weatherRadar;
    private readonly ConcurrentDictionary<string, RoutingNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RoutingEdge> _edges = new(StringComparer.OrdinalIgnoreCase);

    public MultiModalRouteOptimizationEngine(DynamicWeatherHazardRadarService weatherRadar)
    {
        _weatherRadar = weatherRadar ?? throw new ArgumentNullException(nameof(weatherRadar));
        SeedAlgerianCorridorNetwork();
    }

    /// <summary>
    /// Registers or updates a routing node.
    /// </summary>
    public void AddNode(RoutingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.NodeId] = node;
    }

    /// <summary>
    /// Registers or updates a directed routing edge.
    /// </summary>
    public void AddEdge(RoutingEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        _edges[edge.EdgeId] = edge;
    }

    /// <summary>
    /// Computes the optimal multi-modal freight route based on specified optimization goal and real-time weather constraints.
    /// </summary>
    public MultiModalRoutePlan CalculateRoute(CalculateRouteRequest request, DateTime? asOfUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_nodes.TryGetValue(request.OriginNodeId, out var originNode))
        {
            throw new ArgumentException($"Origin node '{request.OriginNodeId}' not found in transport network.", nameof(request));
        }

        if (!_nodes.TryGetValue(request.DestinationNodeId, out var destinationNode))
        {
            throw new ArgumentException($"Destination node '{request.DestinationNodeId}' not found in transport network.", nameof(request));
        }

        if (string.Equals(request.OriginNodeId, request.DestinationNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return new MultiModalRoutePlan(
                RouteId: $"RTE-ZERO-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                OriginNodeId: request.OriginNodeId,
                DestinationNodeId: request.DestinationNodeId,
                Goal: request.Goal,
                TotalDistanceKm: 0.0,
                TotalDurationHours: 0.0,
                TotalCostDzd: 0m,
                TotalCarbonKg: 0.0,
                Legs: [],
                Transfers: [],
                WeatherReroutingApplied: false
            );
        }

        var now = asOfUtc ?? DateTime.UtcNow;
        var weatherAvoidanceActive = request.AvoidWeatherHazards;
        var weatherRerouteOccurred = false;

        // Dijkstra's algorithm with priority queue
        var distances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var previousEdges = new Dictionary<string, (RoutingEdge Edge, RouteLeg Leg)>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var priorityQueue = new PriorityQueue<string, double>();

        foreach (var nodeId in _nodes.Keys)
        {
            distances[nodeId] = double.PositiveInfinity;
        }

        distances[request.OriginNodeId] = 0.0;
        priorityQueue.Enqueue(request.OriginNodeId, 0.0);

        while (priorityQueue.Count > 0)
        {
            var currentId = priorityQueue.Dequeue();

            if (visited.Contains(currentId)) continue;
            visited.Add(currentId);

            if (string.Equals(currentId, request.DestinationNodeId, StringComparison.OrdinalIgnoreCase))
            {
                break; // Found optimal destination path
            }

            // Find all outgoing edges
            var outgoingEdges = _edges.Values.Where(e => string.Equals(e.FromNodeId, currentId, StringComparison.OrdinalIgnoreCase));

            foreach (var edge in outgoingEdges)
            {
                if (visited.Contains(edge.ToNodeId)) continue;

                if (!_nodes.TryGetValue(edge.FromNodeId, out var fromN) ||
                    !_nodes.TryGetValue(edge.ToNodeId, out var toN))
                {
                    continue;
                }

                var (isImpaired, isBlocked, effectiveSpeed, hazardId) = weatherAvoidanceActive
                    ? _weatherRadar.EvaluateEdgeHazard(edge, fromN, toN, now)
                    : (false, false, edge.BaseSpeedKmh, null);

                if (isBlocked)
                {
                    weatherRerouteOccurred = true;
                    continue; // Impassable corridor due to sandstorm or flood
                }

                if (isImpaired)
                {
                    weatherRerouteOccurred = true;
                }

                // Compute leg duration taking into account effective speed and road gradient
                var speedAdjustment = Math.Max(effectiveSpeed * (1.0 - Math.Max(0, edge.GradientPercent * 0.02)), 15.0);
                var transitHours = edge.DistanceKm / speedAdjustment;

                // Modal transfer dwell time penalty if switching modes
                var transferPenaltyHours = 0.0;
                var transferPenaltyCost = 0m;

                if (previousEdges.TryGetValue(edge.FromNodeId, out var prev) && prev.Edge.Mode != edge.Mode)
                {
                    transferPenaltyHours = edge.Mode switch
                    {
                        TransportMode.RailFreight => 2.5,
                        TransportMode.CoastalMaritime => 4.0,
                        _ => 1.5
                    };
                    transferPenaltyCost = 15000m; // Transshipment crane fee
                }

                var legCost = (edge.CostPerKmDzd * (decimal)edge.DistanceKm) + transferPenaltyCost;
                var legCarbonKg = (edge.CarbonGramsPerTonKm * request.CargoWeightTons * edge.DistanceKm) / 1000.0;

                // Compute objective weight
                var edgeWeight = request.Goal switch
                {
                    OptimizationGoal.FastestTime => transitHours + transferPenaltyHours,
                    OptimizationGoal.LowestCost => (double)legCost,
                    OptimizationGoal.LowestCarbon => legCarbonKg,
                    OptimizationGoal.Balanced => (transitHours * 100.0) + ((double)legCost / 100.0) + (legCarbonKg * 5.0),
                    _ => transitHours
                };

                var tentativeDistance = distances[currentId] + edgeWeight;
                if (tentativeDistance < distances[edge.ToNodeId])
                {
                    distances[edge.ToNodeId] = tentativeDistance;

                    var leg = new RouteLeg(
                        FromNodeId: edge.FromNodeId,
                        ToNodeId: edge.ToNodeId,
                        Mode: edge.Mode,
                        DistanceKm: edge.DistanceKm,
                        DurationHours: transitHours,
                        CostDzd: legCost,
                        CarbonKg: legCarbonKg,
                        IsWeatherImpaired: isImpaired,
                        AppliedHazardId: hazardId
                    );

                    previousEdges[edge.ToNodeId] = (edge, leg);
                    priorityQueue.Enqueue(edge.ToNodeId, tentativeDistance);
                }
            }
        }

        if (!previousEdges.ContainsKey(request.DestinationNodeId))
        {
            throw new InvalidOperationException($"No viable route found between '{request.OriginNodeId}' and '{request.DestinationNodeId}' under current network and weather conditions.");
        }

        // Reconstruct path
        var pathLegs = new List<RouteLeg>();
        var transfers = new List<TransshipmentTransfer>();
        var currNode = request.DestinationNodeId;

        while (previousEdges.TryGetValue(currNode, out var record))
        {
            pathLegs.Add(record.Leg);
            currNode = record.Edge.FromNodeId;
        }

        pathLegs.Reverse();

        // Extract transshipment transfer points
        for (var i = 0; i < pathLegs.Count - 1; i++)
        {
            if (pathLegs[i].Mode != pathLegs[i + 1].Mode)
            {
                var hubId = pathLegs[i].ToNodeId;
                transfers.Add(new TransshipmentTransfer(
                    HubNodeId: hubId,
                    FromMode: pathLegs[i].Mode,
                    ToMode: pathLegs[i + 1].Mode,
                    DwellTimeHours: pathLegs[i + 1].Mode == TransportMode.CoastalMaritime ? 4.0 : 2.5,
                    HandlingCostDzd: 15000m
                ));
            }
        }

        var totalDist = pathLegs.Sum(l => l.DistanceKm);
        var totalDur = pathLegs.Sum(l => l.DurationHours) + transfers.Sum(t => t.DwellTimeHours);
        var totalCost = pathLegs.Sum(l => l.CostDzd) + transfers.Sum(t => t.HandlingCostDzd);
        var totalCarbon = pathLegs.Sum(l => l.CarbonKg);

        return new MultiModalRoutePlan(
            RouteId: $"RTE-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            OriginNodeId: request.OriginNodeId,
            DestinationNodeId: request.DestinationNodeId,
            Goal: request.Goal,
            TotalDistanceKm: Math.Round(totalDist, 2),
            TotalDurationHours: Math.Round(totalDur, 2),
            TotalCostDzd: totalCost,
            TotalCarbonKg: Math.Round(totalCarbon, 2),
            Legs: pathLegs,
            Transfers: transfers,
            WeatherReroutingApplied: weatherRerouteOccurred
        );
    }

    /// <summary>
    /// Seeds realistic Algerian transport corridors with road, rail, and maritime nodes and edges.
    /// </summary>
    private void SeedAlgerianCorridorNetwork()
    {
        // Key Logistics Nodes
        var algiers = new RoutingNode("NODE-ALG", "Port of Algiers (Central Hub)", new GeoCoordinate(36.7538, 3.0588, 10.0), true, [TransportMode.RoadFreight, TransportMode.RailFreight, TransportMode.CoastalMaritime]);
        var oran = new RoutingNode("NODE-ORN", "Port of Oran (Western Hub)", new GeoCoordinate(35.6987, -0.6349, 15.0), true, [TransportMode.RoadFreight, TransportMode.RailFreight, TransportMode.CoastalMaritime]);
        var constantine = new RoutingNode("NODE-CZL", "Constantine Inland Dry Port", new GeoCoordinate(36.3650, 6.6147, 650.0), true, [TransportMode.RoadFreight, TransportMode.RailFreight]);
        var annaba = new RoutingNode("NODE-AAE", "Port of Annaba (Eastern Hub)", new GeoCoordinate(36.9000, 7.7667, 8.0), true, [TransportMode.RoadFreight, TransportMode.RailFreight, TransportMode.CoastalMaritime]);
        var biskra = new RoutingNode("NODE-BSK", "Biskra Gateway to the Sahara", new GeoCoordinate(34.8504, 5.7280, 87.0), true, [TransportMode.RoadFreight, TransportMode.RailFreight]);
        var hassiMessaoud = new RoutingNode("NODE-HME", "Hassi Messaoud Industrial Basin", new GeoCoordinate(31.6804, 6.0728, 152.0), false, [TransportMode.RoadFreight]);
        var tamanrasset = new RoutingNode("NODE-TMR", "Tamanrasset Trans-Sahara Terminal", new GeoCoordinate(22.7850, 5.5228, 1370.0), false, [TransportMode.RoadFreight]);

        AddNode(algiers);
        AddNode(oran);
        AddNode(constantine);
        AddNode(annaba);
        AddNode(biskra);
        AddNode(hassiMessaoud);
        AddNode(tamanrasset);

        // East-West Highway Corridor (Road)
        AddEdge(new RoutingEdge("EDGE-RD-ALG-ORN", "NODE-ALG", "NODE-ORN", TransportMode.RoadFreight, 415.0, 90.0, 45m, 1.2, 110.0));
        AddEdge(new RoutingEdge("EDGE-RD-ORN-ALG", "NODE-ORN", "NODE-ALG", TransportMode.RoadFreight, 415.0, 90.0, 45m, 1.2, 110.0));
        AddEdge(new RoutingEdge("EDGE-RD-ALG-CZL", "NODE-ALG", "NODE-CZL", TransportMode.RoadFreight, 390.0, 85.0, 48m, 3.5, 120.0));
        AddEdge(new RoutingEdge("EDGE-RD-CZL-ALG", "NODE-CZL", "NODE-ALG", TransportMode.RoadFreight, 390.0, 85.0, 48m, 3.5, 120.0));
        AddEdge(new RoutingEdge("EDGE-RD-CZL-AAE", "NODE-CZL", "NODE-AAE", TransportMode.RoadFreight, 155.0, 80.0, 42m, 2.0, 105.0));
        AddEdge(new RoutingEdge("EDGE-RD-AAE-CZL", "NODE-AAE", "NODE-CZL", TransportMode.RoadFreight, 155.0, 80.0, 42m, 2.0, 105.0));

        // National Rail Spine (SNTF Rail Freight)
        AddEdge(new RoutingEdge("EDGE-RL-ALG-ORN", "NODE-ALG", "NODE-ORN", TransportMode.RailFreight, 422.0, 75.0, 22m, 0.8, 28.0));
        AddEdge(new RoutingEdge("EDGE-RL-ORN-ALG", "NODE-ORN", "NODE-ALG", TransportMode.RailFreight, 422.0, 75.0, 22m, 0.8, 28.0));
        AddEdge(new RoutingEdge("EDGE-RL-ALG-CZL", "NODE-ALG", "NODE-CZL", TransportMode.RailFreight, 460.0, 70.0, 24m, 1.5, 30.0));
        AddEdge(new RoutingEdge("EDGE-RL-CZL-ALG", "NODE-CZL", "NODE-ALG", TransportMode.RailFreight, 460.0, 70.0, 24m, 1.5, 30.0));
        AddEdge(new RoutingEdge("EDGE-RL-CZL-BSK", "NODE-CZL", "NODE-BSK", TransportMode.RailFreight, 210.0, 65.0, 20m, 1.0, 25.0));
        AddEdge(new RoutingEdge("EDGE-RL-BSK-CZL", "NODE-BSK", "NODE-CZL", TransportMode.RailFreight, 210.0, 65.0, 20m, 1.0, 25.0));

        // Mediterranean Coastal Maritime Feeders (CNAN)
        AddEdge(new RoutingEdge("EDGE-MAR-ALG-ORN", "NODE-ALG", "NODE-ORN", TransportMode.CoastalMaritime, 380.0, 32.0, 12m, 0.0, 18.0));
        AddEdge(new RoutingEdge("EDGE-MAR-ORN-ALG", "NODE-ORN", "NODE-ALG", TransportMode.CoastalMaritime, 380.0, 32.0, 12m, 0.0, 18.0));
        AddEdge(new RoutingEdge("EDGE-MAR-ALG-AAE", "NODE-ALG", "NODE-AAE", TransportMode.CoastalMaritime, 440.0, 32.0, 14m, 0.0, 20.0));
        AddEdge(new RoutingEdge("EDGE-MAR-AAE-ALG", "NODE-AAE", "NODE-ALG", TransportMode.CoastalMaritime, 440.0, 32.0, 14m, 0.0, 20.0));

        // Trans-Sahara Highway & Southern Corridors
        AddEdge(new RoutingEdge("EDGE-RD-BSK-HME", "NODE-BSK", "NODE-HME", TransportMode.RoadFreight, 350.0, 85.0, 52m, 0.5, 115.0));
        AddEdge(new RoutingEdge("EDGE-RD-HME-BSK", "NODE-HME", "NODE-BSK", TransportMode.RoadFreight, 350.0, 85.0, 52m, 0.5, 115.0));
        AddEdge(new RoutingEdge("EDGE-RD-HME-TMR", "NODE-HME", "NODE-TMR", TransportMode.RoadFreight, 1080.0, 75.0, 65m, 1.1, 130.0));
        AddEdge(new RoutingEdge("EDGE-RD-TMR-HME", "NODE-TMR", "NODE-HME", TransportMode.RoadFreight, 1080.0, 75.0, 65m, 1.1, 130.0));
    }
}
