using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Modules.AI.Firewall;
using Xunit;

namespace SGrowing.Tests.AI;

public class SemanticVectorToolCacheServiceTests
{
    private readonly SemanticVectorToolCacheService _cacheService;

    public SemanticVectorToolCacheServiceTests()
    {
        var mockLogger = new Mock<ILogger<SemanticVectorToolCacheService>>();
        _cacheService = new SemanticVectorToolCacheService(mockLogger.Object);
        _cacheService.Clear();
    }

    [Fact]
    public void CalculateCosineSimilarity_CalculatesExpectedGeometricRelationships()
    {
        // 1. Identical vectors -> 1.0
        float[] v1 = [1.0f, 2.0f, 3.0f];
        float[] v2 = [1.0f, 2.0f, 3.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(v1, v2).Should().BeApproximately(1.0, 0.001);

        // 2. Orthogonal vectors -> 0.0
        float[] vOrthA = [1.0f, 0.0f];
        float[] vOrthB = [0.0f, 1.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(vOrthA, vOrthB).Should().BeApproximately(0.0, 0.001);

        // 3. Opposite vectors -> -1.0
        float[] vOppA = [1.0f, 0.0f];
        float[] vOppB = [-1.0f, 0.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(vOppA, vOppB).Should().BeApproximately(-1.0, 0.001);

        // 4. Mismatched length returns 0.0
        float[] vShort = [1.0f];
        float[] vLong = [1.0f, 2.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(vShort, vLong).Should().Be(0.0);

        // 5. Zero vector returns 0.0
        float[] vZero = [0.0f, 0.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(vZero, v1).Should().Be(0.0);
    }

    [Fact]
    public async Task LookupAsync_WhenCacheEmpty_ReturnsMiss()
    {
        var tenantId = Guid.NewGuid();
        float[] queryVec = [0.1f, 0.2f, 0.3f];

        var result = await _cacheService.LookupAsync(tenantId, "crm_search_leads", queryVec);

        result.IsHit.Should().BeFalse();
        result.SimilarityScore.Should().Be(0.0);
        result.CachedEntry.Should().BeNull();
        result.TokensSaved.Should().Be(0);
    }

    [Fact]
    public async Task StoreAsync_And_LookupAsync_WhenSimilarityAboveThreshold_ReturnsHit()
    {
        var tenantId = Guid.NewGuid();
        float[] originalEmbedding = [1.0f, 0.0f, 0.0f];
        float[] queryEmbedding = [0.999f, 0.001f, 0.0f]; // Extremely close vector

        await _cacheService.StoreAsync(
            tenantId: tenantId,
            toolName: "courier_generate_waybill",
            queryEmbedding: originalEmbedding,
            queryParametersJson: "{\"orderId\":\"ORD-123\"}",
            toolResultJson: "{\"waybillNumber\":\"WB-9988\"}",
            originalLatencyMs: 450,
            estimatedPromptTokens: 150,
            estimatedResponseTokens: 80,
            entityId: "ORD-123"
        );

        var lookup = await _cacheService.LookupAsync(
            tenantId,
            "courier_generate_waybill",
            queryEmbedding,
            similarityThreshold: 0.95
        );

        lookup.IsHit.Should().BeTrue();
        lookup.SimilarityScore.Should().BeGreaterThanOrEqualTo(0.95);
        lookup.CachedEntry.Should().NotBeNull();
        lookup.CachedEntry!.ToolResultJson.Should().Contain("WB-9988");
        lookup.TokensSaved.Should().Be(230); // 150 + 80
        lookup.LatencySavedMs.Should().BeGreaterThan(0);

        // Verify metrics
        var metrics = _cacheService.GetMetrics(tenantId);
        metrics.TotalHits.Should().Be(1);
        metrics.TotalTokensSaved.Should().Be(230);
    }

    [Fact]
    public async Task LookupAsync_WhenSimilarityBelowThreshold_ReturnsMiss()
    {
        var tenantId = Guid.NewGuid();
        float[] storedEmbedding = [1.0f, 0.0f, 0.0f];
        float[] queryEmbedding = [0.0f, 1.0f, 0.0f]; // Orthogonal vector

        await _cacheService.StoreAsync(
            tenantId,
            "seo_execute_multipage_audit",
            storedEmbedding,
            "{}",
            "{}",
            200, 100, 50
        );

        var lookup = await _cacheService.LookupAsync(
            tenantId,
            "seo_execute_multipage_audit",
            queryEmbedding,
            similarityThreshold: 0.95
        );

        lookup.IsHit.Should().BeFalse();
        lookup.SimilarityScore.Should().BeLessThan(0.95);
        lookup.CachedEntry.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateByTool_RemovesEntriesForSpecifiedToolOnly()
    {
        var tenantId = Guid.NewGuid();
        float[] vec = [1.0f, 0.0f];

        await _cacheService.StoreAsync(tenantId, "ToolAlpha", vec, "{}", "{}", 100, 10, 10);
        await _cacheService.StoreAsync(tenantId, "ToolBeta", vec, "{}", "{}", 100, 10, 10);

        var removed = _cacheService.InvalidateByTool(tenantId, "ToolAlpha");
        removed.Should().Be(1);

        var lookupAlpha = await _cacheService.LookupAsync(tenantId, "ToolAlpha", vec);
        lookupAlpha.IsHit.Should().BeFalse();

        var lookupBeta = await _cacheService.LookupAsync(tenantId, "ToolBeta", vec);
        lookupBeta.IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidateByEntity_RemovesEntriesMatchingEntityId()
    {
        var tenantId = Guid.NewGuid();
        float[] vec = [1.0f, 0.0f];

        await _cacheService.StoreAsync(tenantId, "ToolAlpha", vec, "{}", "{}", 100, 10, 10, entityId: "ORDER-777");

        var removed = _cacheService.InvalidateByEntity(tenantId, "ORDER-777");
        removed.Should().Be(1);

        var lookup = await _cacheService.LookupAsync(tenantId, "ToolAlpha", vec);
        lookup.IsHit.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateByTenant_RemovesAllTenantEntries()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        float[] vec = [1.0f, 0.0f];

        await _cacheService.StoreAsync(tenant1, "ToolA", vec, "{}", "{}", 100, 10, 10);
        await _cacheService.StoreAsync(tenant2, "ToolA", vec, "{}", "{}", 100, 10, 10);

        var removed = _cacheService.InvalidateByTenant(tenant1);
        removed.Should().Be(1);

        var lookup1 = await _cacheService.LookupAsync(tenant1, "ToolA", vec);
        lookup1.IsHit.Should().BeFalse();

        var lookup2 = await _cacheService.LookupAsync(tenant2, "ToolA", vec);
        lookup2.IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task LookupAsync_NullOrEmptyEmbedding_ReturnsMissAndIncrementsMissCount()
    {
        var tenantId = Guid.NewGuid();

        var nullResult = await _cacheService.LookupAsync(tenantId, "ToolTest", null!);
        nullResult.IsHit.Should().BeFalse();
        nullResult.CachedEntry.Should().BeNull();

        var emptyResult = await _cacheService.LookupAsync(tenantId, "ToolTest", []);
        emptyResult.IsHit.Should().BeFalse();
        emptyResult.CachedEntry.Should().BeNull();

        var metrics = _cacheService.GetMetrics(tenantId);
        metrics.TotalMisses.Should().Be(2);
    }
}
