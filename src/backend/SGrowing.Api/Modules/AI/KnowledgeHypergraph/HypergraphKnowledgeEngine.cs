using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.AI.KnowledgeHypergraph;

/// <summary>
/// In-memory enterprise knowledge hypergraph supporting typed semantic entities,
/// weighted directional edges, k-hop subgraph extraction, and shortest relationship pathfinding.
/// </summary>
public sealed class HypergraphKnowledgeEngine
{
    private readonly ConcurrentDictionary<string, HypergraphNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HypergraphEdge> _edges = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _outgoingAdjacency = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _incomingAdjacency = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _lock = new();

    public void AddNode(HypergraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.NodeId] = node;
        _outgoingAdjacency.TryAdd(node.NodeId, []);
        _incomingAdjacency.TryAdd(node.NodeId, []);
    }

    public void AddEdge(HypergraphEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (!_nodes.ContainsKey(edge.FromNodeId) || !_nodes.ContainsKey(edge.ToNodeId))
        {
            throw new InvalidOperationException($"Both endpoints ('{edge.FromNodeId}' and '{edge.ToNodeId}') must exist in hypergraph before adding an edge.");
        }

        lock (_lock)
        {
            _edges[edge.EdgeId] = edge;

            if (!_outgoingAdjacency.TryGetValue(edge.FromNodeId, out var outSet))
            {
                outSet = [];
                _outgoingAdjacency[edge.FromNodeId] = outSet;
            }
            outSet.Add(edge.EdgeId);

            if (!_incomingAdjacency.TryGetValue(edge.ToNodeId, out var inSet))
            {
                inSet = [];
                _incomingAdjacency[edge.ToNodeId] = inSet;
            }
            inSet.Add(edge.EdgeId);
        }
    }

    public HypergraphNode? GetNode(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    public IReadOnlyList<HypergraphNode> GetAllNodes() => _nodes.Values.ToList();

    public IReadOnlyList<HypergraphEdge> GetAllEdges() => _edges.Values.ToList();

    /// <summary>
    /// Extracts a k-hop subgraph surrounding the specified seed node using BFS.
    /// </summary>
    public ExtractedSubgraph ExtractSubgraph(string seedNodeId, int maxHops = 2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedNodeId);

        if (!_nodes.TryGetValue(seedNodeId, out var seedNode))
        {
            throw new KeyNotFoundException($"Seed node '{seedNodeId}' does not exist in hypergraph.");
        }

        var visitedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { seedNodeId };
        var collectedEdgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((seedNodeId, 0));

        while (queue.Count > 0)
        {
            var (currentNodeId, currentDepth) = queue.Dequeue();

            if (currentDepth >= maxHops)
            {
                continue;
            }

            // Inspect outgoing edges
            if (_outgoingAdjacency.TryGetValue(currentNodeId, out var outEdges))
            {
                foreach (var edgeId in outEdges)
                {
                    if (_edges.TryGetValue(edgeId, out var edge))
                    {
                        collectedEdgeIds.Add(edgeId);
                        if (visitedNodeIds.Add(edge.ToNodeId))
                        {
                            queue.Enqueue((edge.ToNodeId, currentDepth + 1));
                        }
                    }
                }
            }

            // Inspect incoming edges (bidirectional semantic context)
            if (_incomingAdjacency.TryGetValue(currentNodeId, out var inEdges))
            {
                foreach (var edgeId in inEdges)
                {
                    if (_edges.TryGetValue(edgeId, out var edge))
                    {
                        collectedEdgeIds.Add(edgeId);
                        if (visitedNodeIds.Add(edge.FromNodeId))
                        {
                            queue.Enqueue((edge.FromNodeId, currentDepth + 1));
                        }
                    }
                }
            }
        }

        var subgraphNodes = visitedNodeIds
            .Select(id => _nodes[id])
            .ToList();

        var subgraphEdges = collectedEdgeIds
            .Select(id => _edges[id])
            .ToList();

        // Calculate simple degree centrality score
        var centrality = subgraphNodes.Count > 1 ? (double)subgraphEdges.Count / (subgraphNodes.Count * (subgraphNodes.Count - 1)) : 1.0;

        return new ExtractedSubgraph(
            SeedNodeId: seedNodeId,
            Nodes: subgraphNodes,
            Edges: subgraphEdges,
            DepthHops: maxHops,
            CentralityScore: Math.Round(centrality, 4)
        );
    }

    /// <summary>
    /// Discovers the shortest directed or undirected relationship path between two entity nodes.
    /// </summary>
    public IReadOnlyList<HypergraphEdge> FindPath(string fromNodeId, string toNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toNodeId);

        if (string.Equals(fromNodeId, toNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fromNodeId };
        var parentEdge = new Dictionary<string, (string PrevNodeId, HypergraphEdge Edge)>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(fromNodeId);

        var found = false;

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();

            if (string.Equals(curr, toNodeId, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }

            if (_outgoingAdjacency.TryGetValue(curr, out var outEdges))
            {
                foreach (var edgeId in outEdges)
                {
                    if (_edges.TryGetValue(edgeId, out var edge))
                    {
                        if (visited.Add(edge.ToNodeId))
                        {
                            parentEdge[edge.ToNodeId] = (curr, edge);
                            queue.Enqueue(edge.ToNodeId);
                        }
                    }
                }
            }
        }

        if (!found)
        {
            return [];
        }

        var path = new List<HypergraphEdge>();
        var backtrack = toNodeId;

        while (parentEdge.TryGetValue(backtrack, out var link))
        {
            path.Add(link.Edge);
            backtrack = link.PrevNodeId;
        }

        path.Reverse();
        return path;
    }
}
