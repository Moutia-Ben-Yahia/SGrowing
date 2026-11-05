using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI.VectorRag;

public record HybridSearchQuery(
    string QueryText,
    string? CategoryFilter = null,
    int TopK = 5,
    int FusionK = 60
);

public record HybridGroundedCitation(
    string DocumentId,
    string Title,
    string Excerpt,
    double RrfScore
);

public record HybridRagGroundedResponse(
    string QueryText,
    string GroundedSynthesis,
    IReadOnlyList<HybridGroundedCitation> Citations,
    IReadOnlyList<FusedSearchItem> RetrievedContext,
    long LatencyMs
);

public interface IHybridRagEngine
{
    Task<IReadOnlyList<FusedSearchItem>> HybridSearchAsync(
        Guid tenantId,
        HybridSearchQuery query,
        CancellationToken ct = default);

    Task<HybridRagGroundedResponse> QueryGroundedAsync(
        Guid tenantId,
        HybridSearchQuery query,
        CancellationToken ct = default);
}

public class HybridRagEngine : IHybridRagEngine
{
    private readonly SGrowingDbContext _db;
    private readonly IVectorRagEngine _vectorEngine;
    private readonly Bm25LexicalScorer _bm25Scorer = new();
    private readonly ILogger<HybridRagEngine>? _logger;

    public HybridRagEngine(
        SGrowingDbContext db,
        IVectorRagEngine? vectorEngine = null,
        ILogger<HybridRagEngine>? logger = null)
    {
        _db = db;
        _vectorEngine = vectorEngine ?? new VectorRagEngine();
        _logger = logger;
    }

    public async Task<IReadOnlyList<FusedSearchItem>> HybridSearchAsync(
        Guid tenantId,
        HybridSearchQuery query,
        CancellationToken ct = default)
    {
        // 1. Fetch tenant document chunks from EF Core (or fall back to default corpus)
        var dbChunks = await _db.DocumentChunks
            .Where(d => d.TenantId == tenantId)
            .Take(200)
            .ToListAsync(ct);

        List<Bm25Document> corpus;
        List<SemanticSearchResult> denseResults;

        if (dbChunks.Count > 0)
        {
            corpus = dbChunks.Select(d => new Bm25Document(
                Id: d.DocumentId.ToString(),
                Title: d.DocumentTitle,
                Content: d.Content,
                Category: "KnowledgeBase"
            )).ToList();

            // Compute in-memory dense vector cosine similarity against query
            var queryEmbedding = GenerateSyntheticEmbedding(query.QueryText);
            denseResults = dbChunks
                .Select(d =>
                {
                    var emb = d.GetEmbedding();
                    var sim = emb.Length > 0 ? ComputeCosineSimilarity(queryEmbedding, emb) : 0.0;
                    return new SemanticSearchResult(
                        ChunkId: d.Id.ToString(),
                        DocumentId: d.DocumentId.ToString(),
                        Title: d.DocumentTitle,
                        Content: d.Content,
                        Category: "KnowledgeBase",
                        SimilarityScore: Math.Round(sim, 3),
                        CosineDistance: Math.Round(1.0 - sim, 3));
                })
                .Where(r => r.SimilarityScore > 0.05)
                .OrderByDescending(r => r.SimilarityScore)
                .Take(query.TopK * 2)
                .ToList();
        }
        else
        {
            // Use VectorRagEngine in-memory seeded corpus
            denseResults = _vectorEngine.SearchSimilar(new SemanticSearchQuery(
                QueryText: query.QueryText,
                CategoryFilter: query.CategoryFilter,
                TopK: query.TopK * 2));

            corpus = new List<Bm25Document>
            {
                new("doc_pricing_battlecard", "Enterprise Pricing & SLAs", "SGrowing Enterprise tier is billed at $12,500/month with unlimited autonomous AI SDR agents, 99.99% uptime SLA, and sub-150ms WebRTC duplex voice latency.", "Pricing"),
                new("doc_security_compliance", "SOC2 Type II & Air-Gapped Deployments", "All self-hosted clusters support cryptographic offline licensing with SHA-256 signatures, zero outbound internet heartbeat, and AES-256-GCM data-at-rest encryption.", "Security"),
                new("doc_crm_connectors", "Two-Way HubSpot & Salesforce Sync", "Contacts, Companies, and Deals are synchronized bidirectionally with automatic deduplication, custom object schema mapping, and conflict resolution rules.", "Integrations")
            };
        }

        // 2. Perform Sparse BM25 Search
        var sparseResults = _bm25Scorer.ScoreCorpus(corpus, query.QueryText, topK: query.TopK * 2);

        // 3. Perform Reciprocal Rank Fusion
        var fused = ReciprocalRankFusion.Fuse(denseResults, sparseResults, k: query.FusionK, topK: query.TopK);

        _logger?.LogInformation("Hybrid RAG search for '{Query}': Dense={DenseCount}, Sparse={SparseCount}, Fused={FusedCount}",
            query.QueryText, denseResults.Count, sparseResults.Count, fused.Count);

        return fused;
    }

    public async Task<HybridRagGroundedResponse> QueryGroundedAsync(
        Guid tenantId,
        HybridSearchQuery query,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var retrieved = await HybridSearchAsync(tenantId, query, ct);
        sw.Stop();

        if (retrieved.Count == 0)
        {
            return new HybridRagGroundedResponse(
                QueryText: query.QueryText,
                GroundedSynthesis: "No relevant documents found in the sovereign knowledge base matching your query.",
                Citations: Array.Empty<HybridGroundedCitation>(),
                RetrievedContext: Array.Empty<FusedSearchItem>(),
                LatencyMs: sw.ElapsedMilliseconds);
        }

        var citations = retrieved.Select(r => new HybridGroundedCitation(
            DocumentId: r.DocumentId,
            Title: r.Title,
            Excerpt: r.Content.Length > 120 ? r.Content[..120] + "..." : r.Content,
            RrfScore: r.RrfScore
        )).ToList();

        var synthesis = $"Based on verified sources [{string.Join(", ", retrieved.Select(r => r.Title))}]: " +
                        string.Join(" ", retrieved.Select(r => r.Content));

        return new HybridRagGroundedResponse(
            QueryText: query.QueryText,
            GroundedSynthesis: synthesis,
            Citations: citations,
            RetrievedContext: retrieved,
            LatencyMs: sw.ElapsedMilliseconds);
    }

    private static float[] GenerateSyntheticEmbedding(string text)
    {
        const int dims = 64;
        var vec = new float[dims];
        if (string.IsNullOrWhiteSpace(text)) return vec;

        var tokens = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var tok in tokens)
        {
            var hash = (uint)tok.GetHashCode();
            for (int i = 0; i < dims; i++)
            {
                var bit = (hash >> (i % 32)) & 1;
                vec[i] += bit == 1 ? 1.0f : -0.5f;
            }
        }

        // L2 Normalize
        double norm = Math.Sqrt(vec.Sum(v => v * v));
        if (norm > 0)
        {
            for (int i = 0; i < dims; i++) vec[i] = (float)(vec[i] / norm);
        }

        return vec;
    }

    private static double ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0.0;
        int len = Math.Min(a.Length, b.Length);
        double dot = 0.0, normA = 0.0, normB = 0.0;

        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom > 0 ? dot / denom : 0.0;
    }
}
