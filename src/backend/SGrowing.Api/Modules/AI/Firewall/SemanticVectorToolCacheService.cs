using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI.Firewall;

public record CachedToolExecution(
    string CacheKey,
    Guid TenantId,
    string ToolName,
    string? EntityId,
    float[] QueryEmbedding,
    string QueryParametersJson,
    string ToolResultJson,
    int OriginalLatencyMs,
    int EstimatedPromptTokens,
    int EstimatedResponseTokens,
    DateTime CachedAtUtc,
    DateTime LastAccessedAtUtc,
    int HitCount
);

public record SemanticCacheLookupResult(
    bool IsHit,
    double SimilarityScore,
    CachedToolExecution? CachedEntry,
    int LatencySavedMs,
    int TokensSaved,
    double EstimatedCostSavedUsd,
    double LookupDurationMs
);

public record SemanticCacheMetrics(
    long TotalLookups,
    long TotalHits,
    long TotalMisses,
    double HitRatio,
    long TotalTokensSaved,
    double TotalCostSavedUsd,
    long TotalLatencySavedMs,
    int TotalCachedEntries
);

public interface ISemanticVectorToolCacheService
{
    Task<SemanticCacheLookupResult> LookupAsync(
        Guid tenantId,
        string toolName,
        float[] queryEmbedding,
        double similarityThreshold = 0.95,
        CancellationToken ct = default);

    Task<CachedToolExecution> StoreAsync(
        Guid tenantId,
        string toolName,
        float[] queryEmbedding,
        string queryParametersJson,
        string toolResultJson,
        int originalLatencyMs,
        int estimatedPromptTokens,
        int estimatedResponseTokens,
        string? entityId = null,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    int InvalidateByTenant(Guid tenantId);
    int InvalidateByTool(Guid tenantId, string toolName);
    int InvalidateByEntity(Guid tenantId, string entityId);
    int Invalidate(Guid tenantId, string? toolName = null, string? entityId = null);
    SemanticCacheMetrics GetMetrics(Guid? tenantId = null);
    void Clear();
}

public class SemanticVectorToolCacheService : ISemanticVectorToolCacheService
{
    private readonly ILogger<SemanticVectorToolCacheService>? _logger;

    // Partitioned cache: (TenantId, ToolName) -> (CacheKey -> CachedToolExecution)
    private readonly ConcurrentDictionary<(Guid TenantId, string ToolName), ConcurrentDictionary<string, CachedToolExecution>> _cache = new();
    
    // Default cost per million tokens: $5.00 (average between GPT-4o input/output)
    private const double CostPerMillionTokens = 5.0;

    // Global Metrics counters
    private long _totalLookups;
    private long _totalHits;
    private long _totalMisses;
    private long _totalTokensSaved;
    private double _totalCostSavedUsd;
    private long _totalLatencySavedMs;

    public SemanticVectorToolCacheService(ILogger<SemanticVectorToolCacheService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates cosine similarity between two vector embeddings: cos(theta) = (A . B) / (||A|| * ||B||)
    /// </summary>
    public static double CalculateCosineSimilarity(ReadOnlySpan<float> vectorA, ReadOnlySpan<float> vectorB)
    {
        if (vectorA.Length == 0 || vectorB.Length == 0 || vectorA.Length != vectorB.Length)
        {
            return 0.0;
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            float a = vectorA[i];
            float b = vectorB[i];
            dotProduct += a * b;
            normA += a * a;
            normB += b * b;
        }

        if (normA <= 0.0 || normB <= 0.0)
        {
            return 0.0;
        }

        double similarity = dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        return Math.Clamp(similarity, -1.0, 1.0);
    }

    public Task<SemanticCacheLookupResult> LookupAsync(
        Guid tenantId,
        string toolName,
        float[] queryEmbedding,
        double similarityThreshold = 0.95,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalLookups);

        if (queryEmbedding == null || queryEmbedding.Length == 0)
        {
            Interlocked.Increment(ref _totalMisses);
            return Task.FromResult(new SemanticCacheLookupResult(
                IsHit: false,
                SimilarityScore: 0.0,
                CachedEntry: null,
                LatencySavedMs: 0,
                TokensSaved: 0,
                EstimatedCostSavedUsd: 0.0,
                LookupDurationMs: sw.Elapsed.TotalMilliseconds
            ));
        }

        var key = (tenantId, toolName.Trim().ToLowerInvariant());
        if (!_cache.TryGetValue(key, out var partition) || partition.IsEmpty)
        {
            Interlocked.Increment(ref _totalMisses);
            sw.Stop();
            return Task.FromResult(new SemanticCacheLookupResult(
                IsHit: false,
                SimilarityScore: 0.0,
                CachedEntry: null,
                LatencySavedMs: 0,
                TokensSaved: 0,
                EstimatedCostSavedUsd: 0.0,
                LookupDurationMs: sw.Elapsed.TotalMilliseconds
            ));
        }

        CachedToolExecution? bestMatch = null;
        double highestSimilarity = -1.0;

        foreach (var entry in partition.Values)
        {
            ct.ThrowIfCancellationRequested();

            var similarity = CalculateCosineSimilarity(queryEmbedding, entry.QueryEmbedding);
            if (similarity > highestSimilarity)
            {
                highestSimilarity = similarity;
                bestMatch = entry;
            }
        }

        sw.Stop();
        var lookupDurationMs = sw.Elapsed.TotalMilliseconds;

        if (bestMatch != null && highestSimilarity >= similarityThreshold)
        {
            // Cache Hit!
            Interlocked.Increment(ref _totalHits);

            var updatedEntry = bestMatch with
            {
                LastAccessedAtUtc = DateTime.UtcNow,
                HitCount = bestMatch.HitCount + 1
            };
            partition[bestMatch.CacheKey] = updatedEntry;

            var latencySaved = Math.Max(0, updatedEntry.OriginalLatencyMs - (int)Math.Ceiling(lookupDurationMs));
            var tokensSaved = updatedEntry.EstimatedPromptTokens + updatedEntry.EstimatedResponseTokens;
            var costSaved = (tokensSaved / 1_000_000.0) * CostPerMillionTokens;

            Interlocked.Add(ref _totalLatencySavedMs, latencySaved);
            Interlocked.Add(ref _totalTokensSaved, tokensSaved);
            lock (this)
            {
                _totalCostSavedUsd += costSaved;
            }

            _logger?.LogInformation(
                "Semantic Vector Cache HIT: Tool '{Tool}', Tenant {TenantId}. Similarity: {Sim:P2} (>= {Threshold:P2}). Latency Saved: {Saved}ms, Tokens: {Tokens}",
                toolName, tenantId, highestSimilarity, similarityThreshold, latencySaved, tokensSaved);

            return Task.FromResult(new SemanticCacheLookupResult(
                IsHit: true,
                SimilarityScore: highestSimilarity,
                CachedEntry: updatedEntry,
                LatencySavedMs: latencySaved,
                TokensSaved: tokensSaved,
                EstimatedCostSavedUsd: costSaved,
                LookupDurationMs: lookupDurationMs
            ));
        }

        Interlocked.Increment(ref _totalMisses);
        return Task.FromResult(new SemanticCacheLookupResult(
            IsHit: false,
            SimilarityScore: Math.Max(0.0, highestSimilarity),
            CachedEntry: null,
            LatencySavedMs: 0,
            TokensSaved: 0,
            EstimatedCostSavedUsd: 0.0,
            LookupDurationMs: lookupDurationMs
        ));
    }

    public Task<CachedToolExecution> StoreAsync(
        Guid tenantId,
        string toolName,
        float[] queryEmbedding,
        string queryParametersJson,
        string toolResultJson,
        int originalLatencyMs,
        int estimatedPromptTokens,
        int estimatedResponseTokens,
        string? entityId = null,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var key = (tenantId, toolName.Trim().ToLowerInvariant());
        var partition = _cache.GetOrAdd(key, _ => new ConcurrentDictionary<string, CachedToolExecution>());

        var cacheKey = $"{tenantId:N}_{toolName}_{Guid.NewGuid():N}";
        var entry = new CachedToolExecution(
            CacheKey: cacheKey,
            TenantId: tenantId,
            ToolName: toolName.Trim(),
            EntityId: entityId?.Trim(),
            QueryEmbedding: [.. queryEmbedding],
            QueryParametersJson: queryParametersJson,
            ToolResultJson: toolResultJson,
            OriginalLatencyMs: originalLatencyMs,
            EstimatedPromptTokens: estimatedPromptTokens,
            EstimatedResponseTokens: estimatedResponseTokens,
            CachedAtUtc: DateTime.UtcNow,
            LastAccessedAtUtc: DateTime.UtcNow,
            HitCount: 0
        );

        partition[cacheKey] = entry;

        _logger?.LogDebug("Stored semantic cache entry for Tool '{Tool}', Tenant {TenantId}, Key '{CacheKey}'.",
            toolName, tenantId, cacheKey);

        return Task.FromResult(entry);
    }

    public int InvalidateByTenant(Guid tenantId)
    {
        int removedCount = 0;
        var keysToRemove = _cache.Keys.Where(k => k.TenantId == tenantId).ToList();

        foreach (var k in keysToRemove)
        {
            if (_cache.TryRemove(k, out var partition))
            {
                removedCount += partition.Count;
            }
        }

        _logger?.LogInformation("Invalidated {Count} semantic cache entries for Tenant {TenantId}.", removedCount, tenantId);
        return removedCount;
    }

    public int InvalidateByTool(Guid tenantId, string toolName)
    {
        var key = (tenantId, toolName.Trim().ToLowerInvariant());
        if (_cache.TryRemove(key, out var partition))
        {
            _logger?.LogInformation("Invalidated {Count} semantic cache entries for Tool '{Tool}', Tenant {TenantId}.",
                partition.Count, toolName, tenantId);
            return partition.Count;
        }
        return 0;
    }

    public int InvalidateByEntity(Guid tenantId, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId)) return 0;

        int removedCount = 0;
        var tenantPartitions = _cache.Where(k => k.Key.TenantId == tenantId).Select(p => p.Value);

        foreach (var partition in tenantPartitions)
        {
            var keysToRemove = partition
                .Where(kv => string.Equals(kv.Value.EntityId, entityId.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var k in keysToRemove)
            {
                if (partition.TryRemove(k, out _))
                {
                    removedCount++;
                }
            }
        }

        _logger?.LogInformation("Invalidated {Count} semantic cache entries for Entity '{EntityId}', Tenant {TenantId}.",
            removedCount, entityId, tenantId);
        return removedCount;
    }

    public int Invalidate(Guid tenantId, string? toolName = null, string? entityId = null)
    {
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            return InvalidateByEntity(tenantId, entityId);
        }

        if (!string.IsNullOrWhiteSpace(toolName))
        {
            return InvalidateByTool(tenantId, toolName);
        }

        return InvalidateByTenant(tenantId);
    }

    public SemanticCacheMetrics GetMetrics(Guid? tenantId = null)
    {
        int totalEntries = tenantId.HasValue
            ? _cache.Where(kv => kv.Key.TenantId == tenantId.Value).Sum(kv => kv.Value.Count)
            : _cache.Sum(kv => kv.Value.Count);

        long lookups = Volatile.Read(ref _totalLookups);
        long hits = Volatile.Read(ref _totalHits);
        long misses = Volatile.Read(ref _totalMisses);
        double ratio = lookups > 0 ? (double)hits / lookups : 0.0;

        return new SemanticCacheMetrics(
            TotalLookups: lookups,
            TotalHits: hits,
            TotalMisses: misses,
            HitRatio: ratio,
            TotalTokensSaved: Volatile.Read(ref _totalTokensSaved),
            TotalCostSavedUsd: _totalCostSavedUsd,
            TotalLatencySavedMs: Volatile.Read(ref _totalLatencySavedMs),
            TotalCachedEntries: totalEntries
        );
    }

    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _totalLookups, 0);
        Interlocked.Exchange(ref _totalHits, 0);
        Interlocked.Exchange(ref _totalMisses, 0);
        Interlocked.Exchange(ref _totalTokensSaved, 0);
        Interlocked.Exchange(ref _totalLatencySavedMs, 0);
        lock (this)
        {
            _totalCostSavedUsd = 0.0;
        }
    }
}
