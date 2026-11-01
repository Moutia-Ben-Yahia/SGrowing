using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.GraphFraud;

public interface IIdentityGraphEngine
{
    GraphNode AddOrUpdateNode(GraphNode node);
    GraphNode? GetNode(string nodeId);
    IReadOnlyList<GraphNode> GetAllNodes();
    GraphEdge AddEdge(GraphEdge edge);
    IReadOnlyList<GraphEdge> GetAllEdges();
    IReadOnlyList<GraphEdge> GetOutEdges(string nodeId);
    IReadOnlyList<GraphEdge> GetInEdges(string nodeId);
    IReadOnlyList<GraphNode> GetNeighbors(string nodeId, int maxHops = 1);
    (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges) GetSubGraph(string centerNodeId, int depth = 1);
    string NormalizeAddress(string rawAddress);
    string ComputeSoundex(string word);
    int ComputeLevenshteinDistance(string source, string target);
    string ResolveCanonicalAddress(string rawAddress);
    LinkEntitiesResponse LinkCustomerEntities(LinkCustomerEntitiesRequest request);
    void SetNodeQuarantine(string nodeId, bool isQuarantined);
    int NodeCount { get; }
    int EdgeCount { get; }
    void Clear();
}

public class IdentityGraphEngine : IIdentityGraphEngine
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GraphEdge>> _outEdges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GraphEdge>> _inEdges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _canonicalAddressMap = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> AddressAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["st"] = "street",
        ["str"] = "street",
        ["ave"] = "avenue",
        ["av"] = "avenue",
        ["blvd"] = "boulevard",
        ["bd"] = "boulevard",
        ["bvd"] = "boulevard",
        ["rd"] = "road",
        ["apt"] = "apartment",
        ["app"] = "apartment",
        ["appr"] = "apartment",
        ["dr"] = "drive",
        ["ln"] = "lane",
        ["ct"] = "court",
        ["pl"] = "place",
        ["sq"] = "square",
        ["rte"] = "route",
        ["no"] = "number",
        ["nr"] = "number",
        ["num"] = "number",
        ["fl"] = "floor",
        ["flr"] = "floor",
        ["imm"] = "immeuble",
        ["res"] = "residence"
    };

    public int NodeCount
    {
        get
        {
            lock (_lock)
            {
                return _nodes.Count;
            }
        }
    }

    public int EdgeCount
    {
        get
        {
            lock (_lock)
            {
                var total = 0;
                foreach (var edges in _outEdges.Values)
                {
                    total += edges.Count;
                }
                return total;
            }
        }
    }

    public GraphNode AddOrUpdateNode(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        lock (_lock)
        {
            if (_nodes.TryGetValue(node.NodeId, out var existing))
            {
                var mergedProperties = new Dictionary<string, string>(existing.Properties, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in node.Properties)
                {
                    mergedProperties[kvp.Key] = kvp.Value;
                }

                var updated = existing with
                {
                    NodeType = node.NodeType,
                    RiskScore = Math.Max(existing.RiskScore, node.RiskScore),
                    IsQuarantined = existing.IsQuarantined || node.IsQuarantined,
                    Properties = mergedProperties,
                    LastSeenAtUtc = node.LastSeenAtUtc ?? DateTime.UtcNow
                };

                _nodes[node.NodeId] = updated;
                return updated;
            }

            _nodes[node.NodeId] = node;
            if (!_outEdges.ContainsKey(node.NodeId)) _outEdges[node.NodeId] = [];
            if (!_inEdges.ContainsKey(node.NodeId)) _inEdges[node.NodeId] = [];
            return node;
        }
    }

    public GraphNode? GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;

        lock (_lock)
        {
            return _nodes.GetValueOrDefault(nodeId);
        }
    }

    public IReadOnlyList<GraphNode> GetAllNodes()
    {
        lock (_lock)
        {
            return [.. _nodes.Values];
        }
    }

    public GraphEdge AddEdge(GraphEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        lock (_lock)
        {
            EnsureNodePlaceholder(edge.SourceNodeId, GraphNodeType.Customer);
            EnsureNodePlaceholder(edge.TargetNodeId, GraphNodeType.DeviceFingerprint);

            if (!_outEdges.TryGetValue(edge.SourceNodeId, out var outList))
            {
                outList = [];
                _outEdges[edge.SourceNodeId] = outList;
            }

            if (!_inEdges.TryGetValue(edge.TargetNodeId, out var inList))
            {
                inList = [];
                _inEdges[edge.TargetNodeId] = inList;
            }

            var existingIndex = outList.FindIndex(e =>
                string.Equals(e.TargetNodeId, edge.TargetNodeId, StringComparison.OrdinalIgnoreCase) &&
                e.RelationType == edge.RelationType);

            if (existingIndex >= 0)
            {
                var existing = outList[existingIndex];
                var updated = existing with
                {
                    Weight = existing.Weight + edge.Weight,
                    CreatedAtUtc = edge.CreatedAtUtc
                };
                outList[existingIndex] = updated;

                var inIndex = inList.FindIndex(e =>
                    string.Equals(e.SourceNodeId, edge.SourceNodeId, StringComparison.OrdinalIgnoreCase) &&
                    e.RelationType == edge.RelationType);
                if (inIndex >= 0)
                {
                    inList[inIndex] = updated;
                }

                return updated;
            }

            outList.Add(edge);
            inList.Add(edge);
            return edge;
        }
    }

    public IReadOnlyList<GraphEdge> GetAllEdges()
    {
        lock (_lock)
        {
            List<GraphEdge> all = [];
            foreach (var list in _outEdges.Values)
            {
                all.AddRange(list);
            }
            return all;
        }
    }

    public IReadOnlyList<GraphEdge> GetOutEdges(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return [];

        lock (_lock)
        {
            return _outEdges.TryGetValue(nodeId, out var list) ? [.. list] : [];
        }
    }

    public IReadOnlyList<GraphEdge> GetInEdges(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return [];

        lock (_lock)
        {
            return _inEdges.TryGetValue(nodeId, out var list) ? [.. list] : [];
        }
    }

    public IReadOnlyList<GraphNode> GetNeighbors(string nodeId, int maxHops = 1)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || maxHops < 1) return [];

        lock (_lock)
        {
            if (!_nodes.ContainsKey(nodeId)) return [];

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nodeId };
            var currentQueue = new Queue<string>();
            currentQueue.Enqueue(nodeId);

            var neighborNodes = new List<GraphNode>();

            for (var hop = 0; hop < maxHops; hop++)
            {
                var levelSize = currentQueue.Count;
                if (levelSize == 0) break;

                for (var i = 0; i < levelSize; i++)
                {
                    var curr = currentQueue.Dequeue();

                    if (_outEdges.TryGetValue(curr, out var outList))
                    {
                        foreach (var edge in outList)
                        {
                            if (visited.Add(edge.TargetNodeId))
                            {
                                if (_nodes.TryGetValue(edge.TargetNodeId, out var node))
                                {
                                    neighborNodes.Add(node);
                                    currentQueue.Enqueue(edge.TargetNodeId);
                                }
                            }
                        }
                    }

                    if (_inEdges.TryGetValue(curr, out var inList))
                    {
                        foreach (var edge in inList)
                        {
                            if (visited.Add(edge.SourceNodeId))
                            {
                                if (_nodes.TryGetValue(edge.SourceNodeId, out var node))
                                {
                                    neighborNodes.Add(node);
                                    currentQueue.Enqueue(edge.SourceNodeId);
                                }
                            }
                        }
                    }
                }
            }

            return neighborNodes;
        }
    }

    public (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges) GetSubGraph(string centerNodeId, int depth = 1)
    {
        if (string.IsNullOrWhiteSpace(centerNodeId) || depth < 1)
        {
            return ([], []);
        }

        lock (_lock)
        {
            if (!_nodes.TryGetValue(centerNodeId, out var centerNode))
            {
                return ([], []);
            }

            var subGraphNodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase)
            {
                [centerNodeId] = centerNode
            };

            var subGraphEdges = new HashSet<GraphEdge>();
            var currentHop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { centerNodeId };

            for (var d = 0; d < depth; d++)
            {
                var nextHop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var nid in currentHop)
                {
                    if (_outEdges.TryGetValue(nid, out var outList))
                    {
                        foreach (var edge in outList)
                        {
                            subGraphEdges.Add(edge);
                            if (!subGraphNodes.ContainsKey(edge.TargetNodeId))
                            {
                                if (_nodes.TryGetValue(edge.TargetNodeId, out var targetNode))
                                {
                                    subGraphNodes[edge.TargetNodeId] = targetNode;
                                    nextHop.Add(edge.TargetNodeId);
                                }
                            }
                        }
                    }

                    if (_inEdges.TryGetValue(nid, out var inList))
                    {
                        foreach (var edge in inList)
                        {
                            subGraphEdges.Add(edge);
                            if (!subGraphNodes.ContainsKey(edge.SourceNodeId))
                            {
                                if (_nodes.TryGetValue(edge.SourceNodeId, out var srcNode))
                                {
                                    subGraphNodes[edge.SourceNodeId] = srcNode;
                                    nextHop.Add(edge.SourceNodeId);
                                }
                            }
                        }
                    }
                }

                currentHop = nextHop;
                if (currentHop.Count == 0) break;
            }

            return ([.. subGraphNodes.Values], [.. subGraphEdges]);
        }
    }

    public void SetNodeQuarantine(string nodeId, bool isQuarantined)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        lock (_lock)
        {
            if (_nodes.TryGetValue(nodeId, out var existing))
            {
                _nodes[nodeId] = existing with { IsQuarantined = isQuarantined };
            }
        }
    }

    public string NormalizeAddress(string rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress)) return string.Empty;

        var sb = new StringBuilder(rawAddress.Length);
        foreach (var ch in rawAddress.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append(' ');
            }
        }

        var tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return string.Empty;

        var normalizedTokens = new string[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            normalizedTokens[i] = AddressAbbreviations.TryGetValue(token, out var expanded) ? expanded : token;
        }

        return string.Join(' ', normalizedTokens);
    }

    public string ComputeSoundex(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return "0000";

        var clean = word.Trim().ToUpperInvariant();
        var letters = new StringBuilder();
        foreach (var c in clean)
        {
            if (char.IsLetter(c)) letters.Append(c);
        }

        if (letters.Length == 0) return "0000";

        var result = new StringBuilder();
        result.Append(letters[0]);

        char prevCode = MapSoundexChar(letters[0]);

        for (var i = 1; i < letters.Length && result.Length < 4; i++)
        {
            char currentCode = MapSoundexChar(letters[i]);

            if (currentCode != '0' && currentCode != prevCode)
            {
                result.Append(currentCode);
            }

            if (currentCode != '0')
            {
                prevCode = currentCode;
            }
        }

        while (result.Length < 4)
        {
            result.Append('0');
        }

        return result.ToString();
    }

    private static char MapSoundexChar(char c) => c switch
    {
        'B' or 'F' or 'P' or 'V' => '1',
        'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => '2',
        'D' or 'T' => '3',
        'L' => '4',
        'M' or 'N' => '5',
        'R' => '6',
        _ => '0'
    };

    public int ComputeLevenshteinDistance(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var s = source.ToLowerInvariant();
        var t = target.ToLowerInvariant();

        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    public string ResolveCanonicalAddress(string rawAddress)
    {
        var normalized = NormalizeAddress(rawAddress);
        if (string.IsNullOrEmpty(normalized)) return "addr:unspecified";

        lock (_lock)
        {
            if (_canonicalAddressMap.TryGetValue(normalized, out var existingId))
            {
                return existingId;
            }

            // Fuzzy matching against existing canonical address strings: Levenshtein distance <= 2
            foreach (var (candAddress, candId) in _canonicalAddressMap)
            {
                var dist = ComputeLevenshteinDistance(normalized, candAddress);
                if (dist <= 2)
                {
                    _canonicalAddressMap[normalized] = candId;
                    return candId;
                }

                // If multi-word addresses with same token count and word-level Soundex match + small edit distance
                var normTokens = normalized.Split(' ');
                var candTokens = candAddress.Split(' ');
                if (normTokens.Length == candTokens.Length && normTokens.Length > 1)
                {
                    var soundexMatches = true;
                    var tokenDistSum = 0;
                    for (var t = 0; t < normTokens.Length; t++)
                    {
                        if (ComputeSoundex(normTokens[t]) != ComputeSoundex(candTokens[t]))
                        {
                            soundexMatches = false;
                            break;
                        }
                        tokenDistSum += ComputeLevenshteinDistance(normTokens[t], candTokens[t]);
                    }

                    if (soundexMatches && tokenDistSum <= 2)
                    {
                        _canonicalAddressMap[normalized] = candId;
                        return candId;
                    }
                }
            }

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..12].ToLowerInvariant();
            var newId = $"addr:{hash}";
            _canonicalAddressMap[normalized] = newId;

            AddOrUpdateNode(new GraphNode
            {
                NodeId = newId,
                NodeType = GraphNodeType.PhysicalAddress,
                Properties = new Dictionary<string, string>
                {
                    ["normalized_address"] = normalized,
                    ["raw_address_sample"] = rawAddress
                },
                RiskScore = 0.0
            });

            return newId;
        }
    }

    public LinkEntitiesResponse LinkCustomerEntities(LinkCustomerEntitiesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(request));
        }

        lock (_lock)
        {
            var customerNodeId = request.CustomerId.StartsWith("customer:", StringComparison.OrdinalIgnoreCase)
                ? request.CustomerId
                : $"customer:{request.CustomerId}";

            var customerProps = request.Metadata != null
                ? new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var custNode = AddOrUpdateNode(new GraphNode
            {
                NodeId = customerNodeId,
                NodeType = GraphNodeType.Customer,
                Properties = customerProps,
                RiskScore = request.InitialCustomerRiskScore
            });

            var createdOrUpdated = 1;
            var edgesCreated = 0;
            var linkedNodeIds = new List<string>();

            // 1. Device Fingerprint
            if (!string.IsNullOrWhiteSpace(request.DeviceFingerprint))
            {
                var devId = request.DeviceFingerprint.StartsWith("device:", StringComparison.OrdinalIgnoreCase)
                    ? request.DeviceFingerprint
                    : $"device:{request.DeviceFingerprint.Trim()}";

                AddOrUpdateNode(new GraphNode
                {
                    NodeId = devId,
                    NodeType = GraphNodeType.DeviceFingerprint,
                    Properties = new Dictionary<string, string> { ["fingerprint"] = request.DeviceFingerprint }
                });
                createdOrUpdated++;

                AddEdge(new GraphEdge
                {
                    SourceNodeId = customerNodeId,
                    TargetNodeId = devId,
                    RelationType = GraphRelationType.PlacedFromDevice,
                    Weight = 1.0
                });
                edgesCreated++;
                linkedNodeIds.Add(devId);
            }

            // 2. Physical Address (with Soundex / Levenshtein resolution)
            if (!string.IsNullOrWhiteSpace(request.PhysicalAddress))
            {
                var addrId = ResolveCanonicalAddress(request.PhysicalAddress);
                linkedNodeIds.Add(addrId);
                createdOrUpdated++;

                AddEdge(new GraphEdge
                {
                    SourceNodeId = customerNodeId,
                    TargetNodeId = addrId,
                    RelationType = GraphRelationType.DeliveredToAddress,
                    Weight = 1.0
                });
                edgesCreated++;
            }

            // 3. Phone Number
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var cleanPhone = new string(request.PhoneNumber.Where(char.IsAsciiDigit).ToArray());
                var phoneId = $"phone:{cleanPhone}";

                AddOrUpdateNode(new GraphNode
                {
                    NodeId = phoneId,
                    NodeType = GraphNodeType.PhoneNumber,
                    Properties = new Dictionary<string, string> { ["phone"] = request.PhoneNumber }
                });
                createdOrUpdated++;

                AddEdge(new GraphEdge
                {
                    SourceNodeId = customerNodeId,
                    TargetNodeId = phoneId,
                    RelationType = GraphRelationType.RegisteredWithPhone,
                    Weight = 1.0
                });
                edgesCreated++;
                linkedNodeIds.Add(phoneId);
            }

            // 4. Payment Method
            if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
            {
                var cardClean = request.PaymentMethod.Trim().Replace(" ", "");
                var cardId = $"card:{cardClean}";

                AddOrUpdateNode(new GraphNode
                {
                    NodeId = cardId,
                    NodeType = GraphNodeType.PaymentMethod,
                    Properties = new Dictionary<string, string> { ["card_ref"] = request.PaymentMethod }
                });
                createdOrUpdated++;

                AddEdge(new GraphEdge
                {
                    SourceNodeId = customerNodeId,
                    TargetNodeId = cardId,
                    RelationType = GraphRelationType.PaidWithCard,
                    Weight = 1.0
                });
                edgesCreated++;
                linkedNodeIds.Add(cardId);
            }

            // 5. Bank RIB
            if (!string.IsNullOrWhiteSpace(request.BankRib))
            {
                var ribClean = new string(request.BankRib.Where(char.IsAsciiDigit).ToArray());
                var ribId = $"rib:{ribClean}";

                AddOrUpdateNode(new GraphNode
                {
                    NodeId = ribId,
                    NodeType = GraphNodeType.BankRib,
                    Properties = new Dictionary<string, string> { ["rib"] = request.BankRib }
                });
                createdOrUpdated++;

                AddEdge(new GraphEdge
                {
                    SourceNodeId = customerNodeId,
                    TargetNodeId = ribId,
                    RelationType = GraphRelationType.TransferredToBank,
                    Weight = 1.0
                });
                edgesCreated++;
                linkedNodeIds.Add(ribId);
            }

            return new LinkEntitiesResponse
            {
                CustomerNodeId = customerNodeId,
                NodesCreatedOrUpdated = createdOrUpdated,
                EdgesCreated = edgesCreated,
                LinkedNodeIds = linkedNodeIds
            };
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _outEdges.Clear();
            _inEdges.Clear();
            _canonicalAddressMap.Clear();
        }
    }

    private void EnsureNodePlaceholder(string nodeId, GraphNodeType defaultType)
    {
        if (!_nodes.ContainsKey(nodeId))
        {
            _nodes[nodeId] = new GraphNode
            {
                NodeId = nodeId,
                NodeType = defaultType,
                RiskScore = 0.0
            };
        }
    }
}
