using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.KnowledgeBase;

public class RagSearchResult
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public float CosineSimilarity { get; set; }
    public double KeywordScore { get; set; }
    public double CombinedRrfScore { get; set; }
    public string? SourceUrl { get; set; }
}

public class GroundedPromptContext
{
    public string SystemInstructions { get; set; } = string.Empty;
    public string GroundedContext { get; set; } = string.Empty;
    public List<RagSearchResult> RetrievedSources { get; set; } = new();
}

public class VectorRagRetrievalService
{
    private readonly SGrowingDbContext _db;
    private readonly DocumentChunkingService _chunkingService;

    public VectorRagRetrievalService(SGrowingDbContext db, DocumentChunkingService chunkingService)
    {
        _db = db;
        _chunkingService = chunkingService;
    }

    public async Task<List<DocumentChunk>> IndexDocumentAsync(
        Guid tenantId,
        Guid documentId,
        string documentTitle,
        string content,
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Remove existing chunks for this document if any
        var existingChunks = await _db.DocumentChunks
            .Where(c => c.TenantId == tenantId && c.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (existingChunks.Count > 0)
        {
            _db.DocumentChunks.RemoveRange(existingChunks);
        }

        // 2. Chunk text
        var segments = _chunkingService.ChunkText(content, targetChunkWords: 80, overlapWords: 15);
        var createdChunks = new List<DocumentChunk>();

        foreach (var seg in segments)
        {
            var embedding = _chunkingService.GenerateEmbedding(seg.Content);
            var chunk = DocumentChunk.Create(
                tenantId,
                documentId,
                documentTitle,
                seg.Index,
                seg.Content,
                embedding,
                sourceUrl);

            createdChunks.Add(chunk);
        }

        await _db.DocumentChunks.AddRangeAsync(createdChunks, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return createdChunks;
    }

    public async Task<List<RagSearchResult>> HybridSearchAsync(
        Guid tenantId,
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<RagSearchResult>();
        }

        var chunks = await _db.DocumentChunks
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return new List<RagSearchResult>();
        }

        var queryVector = _chunkingService.GenerateEmbedding(query);
        var queryWords = query.ToLowerInvariant()
            .Split(new[] { ' ', '?', '!', ',', '.', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();

        // Compute scores
        var intermediate = new List<(DocumentChunk Chunk, float Cosine, double Keyword)>();

        foreach (var chunk in chunks)
        {
            var chunkVector = chunk.GetEmbedding();
            float cosine = DocumentChunkingService.CalculateCosineSimilarity(queryVector, chunkVector);

            double keywordScore = 0;
            var lowerContent = chunk.Content.ToLowerInvariant();
            var lowerTitle = chunk.DocumentTitle.ToLowerInvariant();

            foreach (var kw in queryWords)
            {
                if (lowerTitle.Contains(kw)) keywordScore += 3.0;
                if (lowerContent.Contains(kw)) keywordScore += 1.0;
            }

            intermediate.Add((chunk, cosine, keywordScore));
        }

        // Rank by Vector
        var rankedByVector = intermediate
            .OrderByDescending(x => x.Cosine)
            .Select((item, idx) => (item.Chunk.Id, VectorRank: idx + 1))
            .ToDictionary(x => x.Id, x => x.VectorRank);

        // Rank by Keyword
        var rankedByKeyword = intermediate
            .OrderByDescending(x => x.Keyword)
            .Select((item, idx) => (item.Chunk.Id, KeywordRank: idx + 1))
            .ToDictionary(x => x.Id, x => x.KeywordRank);

        // Reciprocal Rank Fusion (RRF) with standard constant k = 60
        var results = new List<RagSearchResult>();
        foreach (var item in intermediate)
        {
            int vRank = rankedByVector[item.Chunk.Id];
            int kRank = rankedByKeyword[item.Chunk.Id];
            double rrfScore = (1.0 / (60.0 + vRank)) + (1.0 / (60.0 + kRank));

            results.Add(new RagSearchResult
            {
                ChunkId = item.Chunk.Id,
                DocumentId = item.Chunk.DocumentId,
                DocumentTitle = item.Chunk.DocumentTitle,
                ChunkIndex = item.Chunk.ChunkIndex,
                Content = item.Chunk.Content,
                CosineSimilarity = item.Cosine,
                KeywordScore = item.Keyword,
                CombinedRrfScore = rrfScore,
                SourceUrl = item.Chunk.SourceUrl
            });
        }

        return results
            .OrderByDescending(r => r.CombinedRrfScore)
            .Take(topK)
            .ToList();
    }

    public async Task<GroundedPromptContext> GenerateGroundedContextAsync(
        Guid tenantId,
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        var hits = await HybridSearchAsync(tenantId, query, topK, cancellationToken);

        if (hits.Count == 0)
        {
            return new GroundedPromptContext
            {
                SystemInstructions = "Answer the customer's question directly and politely. If unknown, offer human agent escalation.",
                GroundedContext = "No relevant knowledge base documents found.",
                RetrievedSources = new List<RagSearchResult>()
            };
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("GROUNDED KNOWLEDGE BASE CONTEXT:");
        for (int i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            sb.AppendLine($"[Source {i + 1}] Title: \"{hit.DocumentTitle}\" (Chunk #{hit.ChunkIndex + 1})");
            sb.AppendLine(hit.Content);
            if (!string.IsNullOrEmpty(hit.SourceUrl))
            {
                sb.AppendLine($"URL: {hit.SourceUrl}");
            }
            sb.AppendLine();
        }

        return new GroundedPromptContext
        {
            SystemInstructions = "You are SGrowing AI Assistant. Answer the customer's question strictly based on the provided Grounded Knowledge Base Context. Cite the source title where appropriate.",
            GroundedContext = sb.ToString().Trim(),
            RetrievedSources = hits
        };
    }
}
