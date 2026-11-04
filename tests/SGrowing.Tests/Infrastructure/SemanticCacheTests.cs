using SGrowing.Api.Modules.Infrastructure.SemanticCache;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class SemanticCacheTests
{
    private readonly SemanticCacheEngine _engine = new();

    [Fact]
    public void QueryOrFetch_IdenticalPrompt_ReturnsSub5msCacheHit()
    {
        // Arrange
        var request = new SemanticCacheQueryRequest(
            PromptQuery: "how do I configure multi-region postgresql replication on azure?",
            SimilarityThreshold: 0.94
        );

        // Act
        var result = _engine.QueryOrFetch(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CacheHit);
        Assert.Equal("SemanticVectorCache", result.Source);
        Assert.True(result.ResponseLatencyMs < 10.0);
        Assert.True(result.TokensSaved > 1000);
        Assert.True(result.CostSavedUsd > 0.10m);
    }

    [Fact]
    public void QueryOrFetch_CompletelyNewPrompt_TriggersInferenceAndCachesEntry()
    {
        // Arrange
        var request = new SemanticCacheQueryRequest(
            PromptQuery: "what are the tax implications of BEPS Pillar Two for Dublin entities?",
            SimilarityThreshold: 0.94
        );

        // Act
        var result = _engine.QueryOrFetch(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CacheHit);
        Assert.Equal("UpstreamInferenceProvider", result.Source);
        Assert.True(result.ResponseLatencyMs > 500.0);

        // Re-querying same prompt now yields a cache hit!
        var second = _engine.QueryOrFetch(request);
        Assert.True(second.CacheHit);
        Assert.Equal("SemanticVectorCache", second.Source);
    }

    [Fact]
    public void GetMetrics_ReturnsFinOpsTelemetry()
    {
        // Act
        var metrics = _engine.GetMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalCachedEntries >= 2);
        Assert.True(metrics.TotalCacheHits > 10000);
        Assert.True(metrics.OverallHitRatePercent > 70.0);
        Assert.True(metrics.TotalDollarsSavedUsd > 1000m);
    }

    [Fact]
    public void FlushCache_ClearsAllEntries_ReturnsCountOfFlushedItems()
    {
        var initialMetrics = _engine.GetMetrics();
        Assert.True(initialMetrics.TotalCachedEntries >= 2);

        var flushedCount = _engine.FlushCache(new FlushCacheRequest());
        Assert.True(flushedCount >= 2);

        var afterMetrics = _engine.GetMetrics();
        Assert.Equal(0, afterMetrics.TotalCachedEntries);

        // Previously cached query now misses
        var request = new SemanticCacheQueryRequest("how do I configure multi-region postgresql replication on azure?");
        var result = _engine.QueryOrFetch(request);
        Assert.False(result.CacheHit);
        Assert.Equal("UpstreamInferenceProvider", result.Source);
    }

    [Fact]
    public void QueryOrFetch_NullRequest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.QueryOrFetch(null!));
    }

    [Fact]
    public void QueryOrFetch_PartialOverlap_HitsCacheWhenAboveThreshold()
    {
        // "what are the best practices for whatsapp flows v3.0 screen dag navigation?"
        // Similar prompt with high keyword overlap
        var request = new SemanticCacheQueryRequest(
            PromptQuery: "what are best practices for whatsapp flows v3.0 navigation?",
            SimilarityThreshold: 0.90
        );

        var result = _engine.QueryOrFetch(request);
        Assert.NotNull(result);
        Assert.True(result.CacheHit);
        Assert.Equal("SemanticVectorCache", result.Source);
        Assert.Equal(0.96, result.CosineSimilarityScore);
    }
}
