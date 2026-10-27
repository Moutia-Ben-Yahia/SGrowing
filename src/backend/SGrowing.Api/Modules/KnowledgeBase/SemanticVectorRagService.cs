using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.KnowledgeBase;

public record SemanticSearchResultDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    string ContentExcerpt,
    double SimilarityScore,
    string? SourceUrl
);

public interface ISemanticVectorRagService
{
    float[] GenerateEmbedding(string text, int dimensions = 64);
    double ComputeCosineSimilarity(float[] vectorA, float[] vectorB);
    Task<int> IndexTenantKnowledgeAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<SemanticSearchResultDto>> SearchKnowledgeAsync(Guid tenantId, string query, int topK = 5, double minScore = 0.10, CancellationToken ct = default);
}

public class SemanticVectorRagService : ISemanticVectorRagService
{
    private readonly SGrowingDbContext _db;

    public SemanticVectorRagService(SGrowingDbContext db)
    {
        _db = db;
    }

    public float[] GenerateEmbedding(string text, int dimensions = 64)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[dimensions];
        }

        var vector = new float[dimensions];
        var words = text.ToLowerInvariant().Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '-', '(', ')', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (word.Length <= 1) continue;

            // Word frequency bucket
            int bucket = GetDeterministicHashCode(word) % dimensions;
            vector[bucket] += 1.0f;

            // Character trigrams for subword/morphological resonance
            for (int i = 0; i <= word.Length - 3; i++)
            {
                var trigram = word.Substring(i, 3);
                int triBucket = GetDeterministicHashCode(trigram) % dimensions;
                vector[triBucket] += 0.5f;
            }
        }

        // L2 Normalize the vector so dot product equals cosine similarity
        double sumSq = 0;
        for (int i = 0; i < dimensions; i++)
        {
            sumSq += vector[i] * vector[i];
        }

        double norm = Math.Sqrt(sumSq);
        if (norm > 1e-6)
        {
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] = (float)(vector[i] / norm);
            }
        }

        return vector;
    }

    public double ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null || vectorA.Length == 0 || vectorB.Length == 0)
        {
            return 0.0;
        }

        int len = Math.Min(vectorA.Length, vectorB.Length);
        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < len; i++)
        {
            dot += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denom < 1e-6) return 0.0;

        // Bounded between -1 and 1
        return Math.Clamp(dot / denom, -1.0, 1.0);
    }

    public async Task<int> IndexTenantKnowledgeAsync(Guid tenantId, CancellationToken ct = default)
    {
        var articles = await _db.HelpArticles
            .Where(a => a.TenantId == tenantId && a.IsPublished)
            .ToListAsync(ct);

        int indexedCount = 0;

        foreach (var article in articles)
        {
            var combinedText = $"{article.Title} {article.ContentMarkdown}";
            var embedding = GenerateEmbedding(combinedText);

            var existingChunk = await _db.DocumentChunks
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.DocumentId == article.Id, ct);

            if (existingChunk != null)
            {
                existingChunk.Content = article.ContentMarkdown;
                existingChunk.DocumentTitle = article.Title;
                existingChunk.SetEmbedding(embedding);
            }
            else
            {
                var chunk = DocumentChunk.Create(
                    tenantId,
                    article.Id,
                    article.Title,
                    0,
                    article.ContentMarkdown,
                    embedding,
                    $"/help/{article.Slug}"
                );
                _db.DocumentChunks.Add(chunk);
            }

            indexedCount++;
        }

        await _db.SaveChangesAsync(ct);
        return indexedCount;
    }

    public async Task<List<SemanticSearchResultDto>> SearchKnowledgeAsync(
        Guid tenantId,
        string query,
        int topK = 5,
        double minScore = 0.10,
        CancellationToken ct = default)
    {
        var queryEmbedding = GenerateEmbedding(query);

        var chunks = await _db.DocumentChunks
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct);

        var scored = new List<SemanticSearchResultDto>();

        foreach (var chunk in chunks)
        {
            var chunkEmbedding = chunk.GetEmbedding();
            if (chunkEmbedding.Length == 0) continue;

            var similarity = ComputeCosineSimilarity(queryEmbedding, chunkEmbedding);
            if (similarity >= minScore)
            {
                var excerpt = chunk.Content.Length > 200
                    ? chunk.Content.Substring(0, 197) + "..."
                    : chunk.Content;

                scored.Add(new SemanticSearchResultDto(
                    chunk.Id,
                    chunk.DocumentId,
                    chunk.DocumentTitle,
                    excerpt,
                    Math.Round(similarity, 4),
                    chunk.SourceUrl
                ));
            }
        }

        return scored
            .OrderByDescending(s => s.SimilarityScore)
            .Take(topK)
            .ToList();
    }

    private static int GetDeterministicHashCode(string str)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in str)
            {
                hash = (hash ^ c) * 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
