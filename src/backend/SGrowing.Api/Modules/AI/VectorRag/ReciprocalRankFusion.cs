using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.AI.VectorRag;

public record FusedSearchItem(
    string DocumentId,
    string Title,
    string Content,
    string Category,
    double DenseScore,
    double SparseScore,
    double RrfScore,
    int DenseRank,
    int SparseRank
);

public static class ReciprocalRankFusion
{
    /// <summary>
    /// Combines and reranks dense vector search results and sparse BM25 search results using
    /// Reciprocal Rank Fusion (RRF) with constant k = 60 (standard TREC benchmark default).
    /// Formula: RRF(d) = sum(1 / (k + rank(d)))
    /// </summary>
    public static IReadOnlyList<FusedSearchItem> Fuse(
        IReadOnlyList<SemanticSearchResult> denseResults,
        IReadOnlyList<Bm25ScoredResult> sparseResults,
        int k = 60,
        int topK = 10)
    {
        var itemMap = new Dictionary<string, (string Title, string Content, string Category, double DenseScore, double SparseScore, int DenseRank, int SparseRank)>();

        // Process Dense Rankings (1-based rank)
        for (int i = 0; i < denseResults.Count; i++)
        {
            var item = denseResults[i];
            int rank = i + 1;
            itemMap[item.DocumentId] = (item.Title, item.Content, item.Category, item.SimilarityScore, 0.0, rank, -1);
        }

        // Process Sparse Rankings (1-based rank)
        for (int i = 0; i < sparseResults.Count; i++)
        {
            var item = sparseResults[i];
            int rank = i + 1;
            if (itemMap.TryGetValue(item.DocumentId, out var existing))
            {
                itemMap[item.DocumentId] = (existing.Title, existing.Content, existing.Category, existing.DenseScore, item.Bm25Score, existing.DenseRank, rank);
            }
            else
            {
                itemMap[item.DocumentId] = (item.Title, item.Content, item.Category, 0.0, item.Bm25Score, -1, rank);
            }
        }

        // Calculate RRF Scores
        var fused = new List<FusedSearchItem>();

        foreach (var (docId, (title, content, category, denseScore, sparseScore, denseRank, sparseRank)) in itemMap)
        {
            double rrfScore = 0.0;
            if (denseRank > 0)
            {
                rrfScore += 1.0 / (k + denseRank);
            }
            if (sparseRank > 0)
            {
                rrfScore += 1.0 / (k + sparseRank);
            }

            fused.Add(new FusedSearchItem(
                DocumentId: docId,
                Title: title,
                Content: content,
                Category: category,
                DenseScore: denseScore,
                SparseScore: sparseScore,
                RrfScore: Math.Round(rrfScore, 6),
                DenseRank: denseRank,
                SparseRank: sparseRank));
        }

        return fused
            .OrderByDescending(f => f.RrfScore)
            .Take(topK)
            .ToList();
    }
}
