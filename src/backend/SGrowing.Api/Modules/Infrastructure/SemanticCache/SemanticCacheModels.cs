using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.SemanticCache;

public record SemanticCacheQueryRequest(
    string PromptQuery,
    string ModelTier = "Claude-3.7-Sonnet",
    double SimilarityThreshold = 0.94
);

public record SemanticCacheEntry(
    string CacheKey,
    string NormalizedPrompt,
    string CachedCompletion,
    int TokenCount,
    double ExecutionCostSavedUsd,
    int AccessHits,
    DateTime CachedAtUtc
);

public record SemanticCacheQueryResult(
    bool CacheHit,
    string Source, // "SemanticVectorCache" or "UpstreamInferenceProvider"
    string Completion,
    double CosineSimilarityScore,
    double ResponseLatencyMs,
    int TokensSaved,
    decimal CostSavedUsd,
    DateTime TimestampUtc
);

public record SemanticCacheMetrics(
    int TotalCachedEntries,
    long TotalCacheHits,
    long TotalCacheMisses,
    double OverallHitRatePercent,
    decimal TotalDollarsSavedUsd,
    long TotalTokensSaved,
    double AverageLatencyCacheHitMs,
    double AverageLatencyCacheMissMs
);

public record FlushCacheRequest(
    string? SpecificPrefix = null
);
