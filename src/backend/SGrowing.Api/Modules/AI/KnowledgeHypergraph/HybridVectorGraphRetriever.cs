using System.Text;

namespace SGrowing.Api.Modules.AI.KnowledgeHypergraph;

/// <summary>
/// Hybrid Graph RAG retriever that fuses dense embedding similarity search with
/// topological k-hop subgraph traversal to generate rich contextual knowledge for LLMs.
/// </summary>
public sealed class HybridVectorGraphRetriever
{
    private readonly HypergraphKnowledgeEngine _graphEngine;

    public HybridVectorGraphRetriever(HypergraphKnowledgeEngine graphEngine)
    {
        _graphEngine = graphEngine ?? throw new ArgumentNullException(nameof(graphEngine));
    }

    /// <summary>
    /// Executes hybrid retrieval: dense vector similarity lookup followed by graph neighborhood expansion.
    /// </summary>
    public HybridGraphRagResult Retrieve(QueryGraphRagRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allNodes = _graphEngine.GetAllNodes();
        var matches = new List<VectorMatchResult>();

        // 1. Vector similarity search across nodes with embeddings
        foreach (var node in allNodes)
        {
            if (node.DenseEmbedding is null || node.DenseEmbedding.Length == 0)
            {
                continue;
            }

            var sim = CalculateCosineSimilarity(request.QueryEmbedding, node.DenseEmbedding);
            if (sim >= request.MinSimilarityThreshold)
            {
                matches.Add(new VectorMatchResult(
                    NodeId: node.NodeId,
                    Label: node.Label,
                    EntityType: node.EntityType,
                    CosineSimilarity: Math.Round(sim, 4)
                ));
            }
        }

        var topSeeds = matches
            .OrderByDescending(m => m.CosineSimilarity)
            .Take(request.MaxVectorSeeds)
            .ToList();

        // 2. Topological subgraph expansion around seed nodes
        var subgraphs = new List<ExtractedSubgraph>();
        var globalNodes = new Dictionary<string, HypergraphNode>(StringComparer.OrdinalIgnoreCase);
        var globalEdges = new Dictionary<string, HypergraphEdge>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in topSeeds)
        {
            var subgraph = _graphEngine.ExtractSubgraph(seed.NodeId, request.MaxGraphHops);
            subgraphs.Add(subgraph);

            foreach (var node in subgraph.Nodes)
            {
                globalNodes[node.NodeId] = node;
            }

            foreach (var edge in subgraph.Edges)
            {
                globalEdges[edge.EdgeId] = edge;
            }
        }

        // 3. Synthesize structured context Markdown
        var sb = new StringBuilder();
        sb.AppendLine($"# Knowledge Hypergraph Context for Query: \"{request.Query}\"");
        sb.AppendLine();
        sb.AppendLine("## Top Semantic Entities Identified");

        if (topSeeds.Count == 0)
        {
            sb.AppendLine("_No direct vector matches above similarity threshold._");
        }
        else
        {
            foreach (var seed in topSeeds)
            {
                sb.AppendLine($"- **{seed.Label}** (`{seed.EntityType}`, NodeId: `{seed.NodeId}`) — Similarity: `{seed.CosineSimilarity:F2}`");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Connected Knowledge Graph & Relationships");

        if (globalEdges.Count == 0)
        {
            sb.AppendLine("_No connected multi-hop relationships found._");
        }
        else
        {
            foreach (var edge in globalEdges.Values)
            {
                var fromLabel = globalNodes.TryGetValue(edge.FromNodeId, out var fn) ? fn.Label : edge.FromNodeId;
                var toLabel = globalNodes.TryGetValue(edge.ToNodeId, out var tn) ? tn.Label : edge.ToNodeId;
                sb.AppendLine($"- **{fromLabel}** -[`{edge.RelationType}`]-> **{toLabel}** (Source: `{edge.SourceDocumentReference}`, Weight: `{edge.Weight:F2}`)");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Entity Properties & Metadata");
        foreach (var node in globalNodes.Values)
        {
            sb.AppendLine($"### {node.Label} (`{node.EntityType}`)");
            foreach (var (k, v) in node.Properties)
            {
                sb.AppendLine($"- **{k}**: {v}");
            }
        }

        return new HybridGraphRagResult(
            Query: request.Query,
            TopVectorMatches: topSeeds,
            ExpandedSubgraphs: subgraphs,
            SynthesizedContextMarkdown: sb.ToString(),
            RetrievedAtUtc: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Computes cosine similarity between two float vectors.
    /// </summary>
    public static double CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0.0;
        }

        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0.0 || normB <= 0.0)
        {
            return 0.0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
