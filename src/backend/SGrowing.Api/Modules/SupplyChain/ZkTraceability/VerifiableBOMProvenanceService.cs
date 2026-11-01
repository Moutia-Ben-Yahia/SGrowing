using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.SupplyChain.ZkTraceability;

/// <summary>
/// Service for building, verifying, and generating cryptographic inclusion proofs
/// over supply chain Merkle provenance graphs (DAGs).
/// </summary>
public sealed class VerifiableBOMProvenanceService
{
    /// <summary>
    /// Computes the cryptographic hash of a discrete provenance node.
    /// </summary>
    public static string ComputeNodeHash(
        string nodeId,
        ProvenanceNodeType nodeType,
        string entityName,
        string locationCountryIso,
        IReadOnlyList<string> parentNodeIds,
        string payloadSha256,
        DateTime timestampUtc,
        IReadOnlyDictionary<string, string> metadata)
    {
        var sortedParents = string.Join(",", parentNodeIds.OrderBy(p => p, StringComparer.Ordinal));
        var sortedMeta = string.Join(";", metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));

        var raw = $"{nodeId}:{(int)nodeType}:{entityName.Trim().ToUpperInvariant()}:{locationCountryIso.Trim().ToUpperInvariant()}:{sortedParents}:{payloadSha256}:{timestampUtc:O}:{sortedMeta}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    /// <summary>
    /// Constructs a validated DAG and computes the overarching Merkle root hash.
    /// </summary>
    public MerkleProvenanceGraph BuildProvenanceGraph(
        string productSku,
        string batchId,
        IReadOnlyList<ProvenanceDagNode> nodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productSku);
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        ArgumentNullException.ThrowIfNull(nodes);

        if (nodes.Count == 0)
        {
            throw new ArgumentException("Provenance graph must have at least one node.", nameof(nodes));
        }

        var nodeDict = new Dictionary<string, ProvenanceDagNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            if (nodeDict.ContainsKey(node.NodeId))
            {
                throw new InvalidOperationException($"Duplicate node identifier detected in DAG: {node.NodeId}");
            }

            // Verify node hash integrity
            var computedHash = ComputeNodeHash(
                node.NodeId,
                node.NodeType,
                node.EntityName,
                node.LocationCountryIso,
                node.ParentNodeIds,
                node.PayloadSha256,
                node.TimestampUtc,
                node.Metadata
            );

            if (!string.Equals(node.NodeHash, computedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Node hash mismatch for '{node.NodeId}'. Expected {computedHash}, got {node.NodeHash}");
            }

            nodeDict[node.NodeId] = node;
        }

        // Validate that all parent references exist within the graph (unless root nodes)
        foreach (var node in nodes)
        {
            foreach (var parentId in node.ParentNodeIds)
            {
                if (!nodeDict.ContainsKey(parentId))
                {
                    throw new InvalidOperationException($"Node '{node.NodeId}' references non-existent parent node '{parentId}' in provenance graph.");
                }
            }
        }

        // Compute Merkle root by ordering leaf/node hashes deterministically
        var sortedHashes = nodes
            .OrderBy(n => n.NodeId, StringComparer.Ordinal)
            .Select(n => n.NodeHash)
            .ToList();

        var rootHash = ZeroKnowledgeOriginProofEngine.ComputeMerkleRootFromHashes(sortedHashes);
        var graphId = $"DAG-{batchId}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();

        return new MerkleProvenanceGraph(
            GraphId: graphId,
            ProductSku: productSku,
            BatchId: batchId,
            MerkleRootHash: rootHash,
            Nodes: nodeDict,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Generates a Merkle inclusion proof for a specified node within a provenance graph.
    /// </summary>
    public MerkleInclusionProof GenerateInclusionProof(MerkleProvenanceGraph graph, string leafNodeId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(leafNodeId);

        if (!graph.Nodes.TryGetValue(leafNodeId, out var targetNode))
        {
            throw new KeyNotFoundException($"Node '{leafNodeId}' not found in provenance graph '{graph.GraphId}'.");
        }

        var sortedNodes = graph.Nodes.Values
            .OrderBy(n => n.NodeId, StringComparer.Ordinal)
            .ToList();

        var leafIndex = sortedNodes.FindIndex(n => string.Equals(n.NodeId, leafNodeId, StringComparison.OrdinalIgnoreCase));
        var leaves = sortedNodes.Select(n => Convert.FromHexString(n.NodeHash)).ToList();

        var auditPath = new List<string>();
        var isLeftNeighborList = new List<bool>();

        var currentIndex = leafIndex;
        var currentLevel = leaves;

        while (currentLevel.Count > 1)
        {
            var isRightSibling = currentIndex % 2 == 1;
            int siblingIndex = isRightSibling ? currentIndex - 1 : currentIndex + 1;

            if (siblingIndex < currentLevel.Count)
            {
                auditPath.Add(Convert.ToHexString(currentLevel[siblingIndex]).ToLowerInvariant());
                isLeftNeighborList.Add(isRightSibling); // if current is right, sibling is left
            }
            else
            {
                // Odd duplicate sibling
                auditPath.Add(Convert.ToHexString(currentLevel[currentIndex]).ToLowerInvariant());
                isLeftNeighborList.Add(false);
            }

            var nextLevel = new List<byte[]>();
            for (var i = 0; i < currentLevel.Count; i += 2)
            {
                if (i + 1 < currentLevel.Count)
                {
                    var combined = new byte[currentLevel[i].Length + currentLevel[i + 1].Length];
                    Buffer.BlockCopy(currentLevel[i], 0, combined, 0, currentLevel[i].Length);
                    Buffer.BlockCopy(currentLevel[i + 1], 0, combined, currentLevel[i].Length, currentLevel[i + 1].Length);
                    nextLevel.Add(SHA256.HashData(combined));
                }
                else
                {
                    var combined = new byte[currentLevel[i].Length * 2];
                    Buffer.BlockCopy(currentLevel[i], 0, combined, 0, currentLevel[i].Length);
                    Buffer.BlockCopy(currentLevel[i], 0, combined, currentLevel[i].Length, currentLevel[i].Length);
                    nextLevel.Add(SHA256.HashData(combined));
                }
            }

            currentIndex /= 2;
            currentLevel = nextLevel;
        }

        var isValid = VerifyInclusionProof(targetNode.NodeHash, auditPath, isLeftNeighborList, graph.MerkleRootHash);

        return new MerkleInclusionProof(
            LeafNodeId: leafNodeId,
            LeafHash: targetNode.NodeHash,
            AuditPathHashes: auditPath,
            IsLeftNeighbor: isLeftNeighborList,
            ExpectedRootHash: graph.MerkleRootHash,
            IsValid: isValid
        );
    }

    /// <summary>
    /// Mathematically verifies a Merkle inclusion proof against a known root hash.
    /// </summary>
    public static bool VerifyInclusionProof(
        string leafHash,
        IReadOnlyList<string> auditPath,
        IReadOnlyList<bool> isLeftNeighbor,
        string expectedRootHash)
    {
        if (string.IsNullOrWhiteSpace(leafHash) || string.IsNullOrWhiteSpace(expectedRootHash))
        {
            return false;
        }

        if (auditPath.Count != isLeftNeighbor.Count)
        {
            return false;
        }

        var currentHash = Convert.FromHexString(leafHash);

        for (var i = 0; i < auditPath.Count; i++)
        {
            var siblingHash = Convert.FromHexString(auditPath[i]);
            var siblingIsLeft = isLeftNeighbor[i];

            byte[] combined;
            if (siblingIsLeft)
            {
                combined = new byte[siblingHash.Length + currentHash.Length];
                Buffer.BlockCopy(siblingHash, 0, combined, 0, siblingHash.Length);
                Buffer.BlockCopy(currentHash, 0, combined, siblingHash.Length, currentHash.Length);
            }
            else
            {
                combined = new byte[currentHash.Length + siblingHash.Length];
                Buffer.BlockCopy(currentHash, 0, combined, 0, currentHash.Length);
                Buffer.BlockCopy(siblingHash, 0, combined, currentHash.Length, siblingHash.Length);
            }

            currentHash = SHA256.HashData(combined);
        }

        var finalComputedRoot = Convert.ToHexString(currentHash).ToLowerInvariant();
        return string.Equals(finalComputedRoot, expectedRootHash, StringComparison.OrdinalIgnoreCase);
    }
}
