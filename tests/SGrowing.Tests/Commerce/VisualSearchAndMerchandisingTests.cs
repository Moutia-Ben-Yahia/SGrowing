using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.VisualSearch;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class VisualSearchAndMerchandisingTests
{
    private readonly VisualEmbeddingSearchEngine _searchEngine = new();
    private readonly VisualColorAndStyleMatcherService _styleMatcher;
    private readonly DynamicShelfMerchandisingService _shelfService;

    public VisualSearchAndMerchandisingTests()
    {
        _styleMatcher = new VisualColorAndStyleMatcherService(_searchEngine);
        _shelfService = new DynamicShelfMerchandisingService(_searchEngine);
    }

    private static float[] CreateVector(int dimensions, float fillValue = 1.0f)
    {
        var vec = new float[dimensions];
        Array.Fill(vec, fillValue);
        return vec;
    }

    private static float[] CreateNormalizedVector(int dimensions, int nonZeroIndex = 0)
    {
        var vec = new float[dimensions];
        if (nonZeroIndex >= 0 && nonZeroIndex < dimensions)
        {
            vec[nonZeroIndex] = 1.0f;
        }
        return vec;
    }

    #region 1. Vector Indexing, Dimensionality Validation & Zero-Vector Handling

    [Fact]
    public void VectorDimensions_Valid512Dimension_ShouldIndexSuccessfully()
    {
        var vector = CreateVector(512, 0.5f);
        var record = new VisualEmbeddingRecord("prod-512", "tenant-1", "Fashion", vector, [], SilhouetteTypes.LooseFit);

        _searchEngine.IndexProductEmbedding(record);

        _searchEngine.TotalIndexed.Should().Be(1);
        _searchEngine.GetProduct("prod-512").Should().NotBeNull();
    }

    [Fact]
    public void VectorDimensions_Valid768Dimension_ShouldIndexSuccessfully()
    {
        var vector = CreateVector(768, 0.25f);
        var record = new VisualEmbeddingRecord("prod-768", "tenant-1", "Fashion", vector, [], SilhouetteTypes.SlimFit);

        _searchEngine.IndexProductEmbedding(record);

        _searchEngine.TotalIndexed.Should().Be(1);
        _searchEngine.GetProduct("prod-768").Should().NotBeNull();
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(0)]
    public void VectorDimensions_UnsupportedDimension_ShouldThrowArgumentException(int dimension)
    {
        var vector = new float[dimension];
        var record = new VisualEmbeddingRecord("invalid-dim", "tenant-1", "Fashion", vector, [], SilhouetteTypes.Boxy);

        var act = () => _searchEngine.IndexProductEmbedding(record);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dimensionality*not supported*");
    }

    [Fact]
    public void VectorDimensions_NullVector_ShouldThrowArgumentNullException()
    {
        var act = () => _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord(
            "null-vec", "tenant-1", "Fashion", null!, [], SilhouetteTypes.Standard));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ZeroVector_Handling_QueryZeroVector_ShouldReturnZeroSimilarityWithoutCrashing()
    {
        var normalVector = CreateVector(512, 1.0f);
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("p-normal", "tenant-1", "Fashion", normalVector, [], SilhouetteTypes.Standard));

        var zeroQuery = new float[512];
        var results = _searchEngine.SearchNearest(zeroQuery, topK: 5);

        results.Should().HaveCount(1);
        results[0].CosineSimilarity.Should().Be(0.0f);
    }

    [Fact]
    public void ZeroVector_Handling_IndexedZeroVector_ShouldYieldZeroSimilarity()
    {
        var zeroVector = new float[512];
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("p-zero", "tenant-1", "Fashion", zeroVector, [], SilhouetteTypes.Standard));

        var query = CreateVector(512, 1.0f);
        var results = _searchEngine.SearchNearest(query, topK: 5);

        results.Should().HaveCount(1);
        results[0].CosineSimilarity.Should().Be(0.0f);
    }

    [Fact]
    public void BatchIndex_MultipleRecords_ShouldIndexAllRecordsUnderLock()
    {
        var records = Enumerable.Range(1, 10).Select(i => new VisualEmbeddingRecord(
            $"batch-prod-{i}",
            "tenant-1",
            "Outerwear",
            CreateVector(512, (float)i),
            [],
            SilhouetteTypes.Standard)).ToList();

        _searchEngine.BatchIndex(records);

        _searchEngine.TotalIndexed.Should().Be(10);
    }

    [Fact]
    public void RemoveProduct_ExistingProduct_ShouldReturnTrueAndDecrementCount()
    {
        var record = new VisualEmbeddingRecord("prod-rem", "tenant-1", "Shoes", CreateVector(512, 1.0f), [], SilhouetteTypes.Standard);
        _searchEngine.IndexProductEmbedding(record);
        _searchEngine.TotalIndexed.Should().Be(1);

        bool removed = _searchEngine.RemoveProduct("prod-rem");

        removed.Should().BeTrue();
        _searchEngine.TotalIndexed.Should().Be(0);
        _searchEngine.GetProduct("prod-rem").Should().BeNull();
    }

    [Fact]
    public void RemoveProduct_NonExistentProduct_ShouldReturnFalse()
    {
        bool removed = _searchEngine.RemoveProduct("non-existent-id");
        removed.Should().BeFalse();
    }

    [Fact]
    public void GetProduct_ReturnsCorrectRecord_OrNullIfAbsent()
    {
        var record = new VisualEmbeddingRecord("prod-get", "tenant-1", "Accessories", CreateVector(768, 0.5f), [], SilhouetteTypes.Boxy);
        _searchEngine.IndexProductEmbedding(record);

        var retrieved = _searchEngine.GetProduct("prod-get");
        retrieved.Should().NotBeNull();
        retrieved!.Category.Should().Be("Accessories");
        retrieved.SilhouetteType.Should().Be(SilhouetteTypes.Boxy);

        _searchEngine.GetProduct("unknown").Should().BeNull();
    }

    [Fact]
    public void Clear_ShouldEmptyVectorIndex()
    {
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("p1", "t1", "C1", CreateVector(512, 1.0f), [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("p2", "t1", "C1", CreateVector(512, 2.0f), [], SilhouetteTypes.Standard));

        _searchEngine.TotalIndexed.Should().Be(2);
        _searchEngine.Clear();
        _searchEngine.TotalIndexed.Should().Be(0);
    }

    #endregion

    #region 2. Cosine Similarity & Nearest Neighbor Retrieval

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ShouldReturnOne()
    {
        var v1 = CreateVector(512, 3.5f);
        var v2 = CreateVector(512, 3.5f);

        float sim = VisualEmbeddingSearchEngine.CalculateCosineSimilarity(v1, v2);

        sim.Should().BeApproximately(1.0f, 0.0001f);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ShouldReturnZero()
    {
        var v1 = CreateNormalizedVector(512, 0); // [1, 0, 0, ...]
        var v2 = CreateNormalizedVector(512, 1); // [0, 1, 0, ...]

        float sim = VisualEmbeddingSearchEngine.CalculateCosineSimilarity(v1, v2);

        sim.Should().BeApproximately(0.0f, 0.0001f);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ShouldBeClampedToZero()
    {
        var v1 = CreateVector(512, 1.0f);
        var v2 = CreateVector(512, -1.0f);

        float sim = VisualEmbeddingSearchEngine.CalculateCosineSimilarity(v1, v2);

        sim.Should().Be(0.0f);
    }

    [Fact]
    public void SearchNearest_TopKOrdering_ClosestVectorShouldHaveRankOneAndHighestSimilarity()
    {
        var target = CreateNormalizedVector(512, 0); // Target at index 0

        // Vector A: Almost identical (index 0 is 0.9, index 1 is 0.1)
        var vecA = new float[512];
        vecA[0] = 0.95f;
        vecA[1] = 0.05f;

        // Vector B: Moderately close (index 0 is 0.5, index 1 is 0.5)
        var vecB = new float[512];
        vecB[0] = 0.5f;
        vecB[1] = 0.5f;

        // Vector C: Orthogonal (index 1 is 1.0)
        var vecC = CreateNormalizedVector(512, 1);

        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("prod-C", "t1", "Fashion", vecC, [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("prod-B", "t1", "Fashion", vecB, [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("prod-A", "t1", "Fashion", vecA, [], SilhouetteTypes.Standard));

        var results = _searchEngine.SearchNearest(target, topK: 3);

        results.Should().HaveCount(3);
        results[0].ProductId.Should().Be("prod-A");
        results[0].Rank.Should().Be(1);
        results[0].CosineSimilarity.Should().BeGreaterThan(results[1].CosineSimilarity);

        results[1].ProductId.Should().Be("prod-B");
        results[1].Rank.Should().Be(2);

        results[2].ProductId.Should().Be("prod-C");
        results[2].Rank.Should().Be(3);
    }

    [Fact]
    public void SearchNearest_TopKLimit_ShouldReturnExactRequestedCount()
    {
        for (int i = 0; i < 20; i++)
        {
            var vec = CreateNormalizedVector(512, i % 512);
            _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord($"prod-{i}", "t1", "Fashion", vec, [], SilhouetteTypes.Standard));
        }

        var results = _searchEngine.SearchNearest(CreateNormalizedVector(512, 0), topK: 7);

        results.Should().HaveCount(7);
        results.Select(r => r.Rank).Should().BeEquivalentTo([1, 2, 3, 4, 5, 6, 7]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SearchNearest_ZeroOrNegativeTopK_ShouldReturnEmptyList(int topK)
    {
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("prod-1", "t1", "Fashion", CreateVector(512, 1.0f), [], SilhouetteTypes.Standard));

        var results = _searchEngine.SearchNearest(CreateVector(512, 1.0f), topK: topK);

        results.Should().BeEmpty();
    }

    [Fact]
    public void SearchNearest_CategoryFilter_ShouldOnlyReturnMatchingCategoryRecords()
    {
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("dress-1", "t1", "Dresses", CreateVector(512, 1.0f), [], SilhouetteTypes.ALine));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("shoes-1", "t1", "Shoes", CreateVector(512, 1.0f), [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("dress-2", "t1", "Dresses", CreateVector(512, 0.9f), [], SilhouetteTypes.SlimFit));

        var results = _searchEngine.SearchNearest(CreateVector(512, 1.0f), topK: 10, categoryFilter: "Dresses");

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Category.Should().Be("Dresses"));
    }

    [Fact]
    public void SearchNearest_DimensionMismatch_ShouldSkipNonMatchingDimensionalityRecords()
    {
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("dim-512", "t1", "Fashion", CreateVector(512, 1.0f), [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("dim-768", "t1", "Fashion", CreateVector(768, 1.0f), [], SilhouetteTypes.Standard));

        var query512 = CreateVector(512, 1.0f);
        var results = _searchEngine.SearchNearest(query512, topK: 10);

        results.Should().HaveCount(1);
        results[0].ProductId.Should().Be("dim-512");
    }

    [Fact]
    public void SearchNearest_DeterministicTieBreaking_ShouldPreserveConsistentOrder()
    {
        var vec = CreateVector(512, 1.0f);
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("prod-Z", "t1", "Fashion", vec, [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("prod-A", "t1", "Fashion", vec, [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("prod-M", "t1", "Fashion", vec, [], SilhouetteTypes.Standard));

        var results = _searchEngine.SearchNearest(vec, topK: 10);

        results.Should().HaveCount(3);
        results[0].ProductId.Should().Be("prod-A");
        results[1].ProductId.Should().Be("prod-M");
        results[2].ProductId.Should().Be("prod-Z");
    }

    #endregion

    #region 3. Color Space Distance & Aesthetics Matching

    [Fact]
    public void RgbDistance_IdenticalColors_ShouldBeZero()
    {
        double dist = _styleMatcher.CalculateRgbDistance(120, 150, 200, 120, 150, 200);
        dist.Should().Be(0.0);
    }

    [Fact]
    public void RgbDistance_BlackAndWhite_ShouldMatchExpectedEuclideanDistance()
    {
        // sqrt(255^2 + 255^2 + 255^2) = 255 * sqrt(3) ~= 441.67
        double dist = _styleMatcher.CalculateRgbDistance("#000000", "#FFFFFF");
        dist.Should().BeApproximately(441.67, 0.05);
    }

    [Fact]
    public void CielabDistance_IdenticalColors_ShouldBeZero()
    {
        double dist = _styleMatcher.CalculateCielabDistance(180, 100, 50, 180, 100, 50);
        dist.Should().Be(0.0);
    }

    [Fact]
    public void CielabDistance_ContrastColors_ShouldProducePositiveDeltaE()
    {
        double dist = _styleMatcher.CalculateCielabDistance("#000000", "#FFFFFF");
        dist.Should().BeGreaterThan(90.0); // Pure black vs white Delta E is ~100
    }

    [Fact]
    public void MapToCanonicalColor_PureBlack_ShouldMapToNoir()
    {
        var dom = _styleMatcher.MapToCanonicalColor(0, 0, 0);
        dom.ColorName.Should().Be(CanonicalCommerceColors.Noir);
        dom.HexCode.Should().Be("#000000");
    }

    [Fact]
    public void MapToCanonicalColor_PureWhite_ShouldMapToBlanc()
    {
        var dom = _styleMatcher.MapToCanonicalColor(255, 255, 255);
        dom.ColorName.Should().Be(CanonicalCommerceColors.Blanc);
        dom.HexCode.Should().Be("#FFFFFF");
    }

    [Fact]
    public void MapToCanonicalColor_BordeauxTone_ShouldMapToBordeaux()
    {
        // Dark deep wine red: R=130, G=10, B=35
        var dom = _styleMatcher.MapToCanonicalColor(130, 10, 35);
        dom.ColorName.Should().Be(CanonicalCommerceColors.Bordeaux);
    }

    [Fact]
    public void MapToCanonicalColor_OliveTone_ShouldMapToOlive()
    {
        // Olive: R=128, G=125, B=5
        var dom = _styleMatcher.MapToCanonicalColor(128, 125, 5);
        dom.ColorName.Should().Be(CanonicalCommerceColors.Olive);
    }

    [Fact]
    public void ExtractColorPalette_FromRgbBytes_ShouldExtractTopColorsAndNormalizeProminence()
    {
        // 60 pixels of black (0,0,0) and 40 pixels of pure white (255,255,255)
        var rgbBytes = new List<byte>();
        for (int i = 0; i < 60; i++)
        {
            rgbBytes.AddRange([0, 0, 0]);
        }
        for (int i = 0; i < 40; i++)
        {
            rgbBytes.AddRange([255, 255, 255]);
        }

        var palette = _styleMatcher.ExtractColorPalette(rgbBytes.ToArray(), maxColors: 2);

        palette.Should().HaveCount(2);
        palette[0].ColorName.Should().Be(CanonicalCommerceColors.Noir);
        palette[0].ProminencePct.Should().BeApproximately(0.60, 0.05);

        palette[1].ColorName.Should().Be(CanonicalCommerceColors.Blanc);
        palette[1].ProminencePct.Should().BeApproximately(0.40, 0.05);

        (palette[0].ProminencePct + palette[1].ProminencePct).Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void ExtractColorPalette_EmptyOrNullData_ShouldReturnFallbackPalette()
    {
        var emptyPalette = _styleMatcher.ExtractColorPalette([]);
        emptyPalette.Should().NotBeEmpty();

        var nullPalette = _styleMatcher.ExtractColorPalette(null!);
        nullPalette.Should().NotBeEmpty();
    }

    [Fact]
    public void MatchByColor_WithinDistanceThreshold_ShouldReturnMatchesOrderedByDistance()
    {
        var navyColor = new DominantColor("#000080", 0, 0, 128, 0.8, CanonicalCommerceColors.Navy);
        var whiteColor = new DominantColor("#FFFFFF", 255, 255, 255, 0.9, CanonicalCommerceColors.Blanc);

        var rec1 = new VisualEmbeddingRecord("navy-suit", "t1", "Suits", CreateVector(512, 1f), [navyColor], SilhouetteTypes.SlimFit);
        var rec2 = new VisualEmbeddingRecord("white-shirt", "t1", "Shirts", CreateVector(512, 1f), [whiteColor], SilhouetteTypes.Standard);

        _searchEngine.IndexProductEmbedding(rec1);
        _searchEngine.IndexProductEmbedding(rec2);

        // Target: Midnight Blue #000085 (very close to Navy)
        var matches = _styleMatcher.MatchByColor("#000085", maxDistance: 25.0);

        matches.Should().HaveCount(1);
        matches[0].ProductId.Should().Be("navy-suit");
        matches[0].ColorDistance.Should().BeLessThan(10.0);
    }

    [Fact]
    public void MatchByColor_ExceedingDistanceThreshold_ShouldReturnEmptyList()
    {
        var blackColor = new DominantColor("#000000", 0, 0, 0, 1.0, CanonicalCommerceColors.Noir);
        var rec = new VisualEmbeddingRecord("black-coat", "t1", "Coats", CreateVector(512, 1f), [blackColor], SilhouetteTypes.LooseFit);
        _searchEngine.IndexProductEmbedding(rec);

        // Searching for bright yellow near black with maxDistance 10.0
        var matches = _styleMatcher.MatchByColor("#FFFF00", maxDistance: 10.0);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void FilterByStyleAndColor_ShouldFilterBySilhouetteAndMaxColorDistance()
    {
        var camel = new DominantColor("#C19A6B", 193, 154, 107, 0.9, CanonicalCommerceColors.Camel);
        var black = new DominantColor("#000000", 0, 0, 0, 0.9, CanonicalCommerceColors.Noir);

        var candidate1 = new VisualEmbeddingRecord("c1", "t1", "Coats", CreateVector(512), [camel], SilhouetteTypes.LooseFit);
        var candidate2 = new VisualEmbeddingRecord("c2", "t1", "Coats", CreateVector(512), [camel], SilhouetteTypes.SlimFit);
        var candidate3 = new VisualEmbeddingRecord("c3", "t1", "Coats", CreateVector(512), [black], SilhouetteTypes.LooseFit);

        var filtered = _styleMatcher.FilterByStyleAndColor(
            [candidate1, candidate2, candidate3],
            targetSilhouette: SilhouetteTypes.Boxy, // LooseFit has 0.88 affinity with Boxy, SlimFit has 0.50
            targetHex: "#C19A6B",
            maxColorDistance: 20.0,
            minStyleAffinity: 0.80);

        filtered.Should().HaveCount(1);
        filtered[0].ProductId.Should().Be("c1");
    }

    #endregion

    #region 4. Color Harmony & Silhouette Affinity Scoring

    [Fact]
    public void CalculateColorHarmony_MonochromaticHues_ShouldReturnHighHarmonyScore()
    {
        // Pure red and slightly darker red
        double harmony = _styleMatcher.CalculateColorHarmony("#FF0000", "#CC0000");
        harmony.Should().BeGreaterThan(0.90);
    }

    [Fact]
    public void CalculateColorHarmony_AnalogousHues_ShouldReturnHighHarmonyScore()
    {
        // Red (0 deg) and Orange (30 deg)
        double harmony = _styleMatcher.CalculateColorHarmony("#FF0000", "#FF8000");
        harmony.Should().BeGreaterThan(0.85);
    }

    [Fact]
    public void CalculateColorHarmony_ComplementaryHues_ShouldReturnHighHarmonyScore()
    {
        // Blue (240 deg) and Yellow/Orange (60 deg) -> ~180 deg difference
        double harmony = _styleMatcher.CalculateColorHarmony("#0000FF", "#FFFF00");
        harmony.Should().BeGreaterThan(0.80);
    }

    [Fact]
    public void CalculateColorHarmony_NeutralColorPairs_ShouldHarmonizeUniversally()
    {
        // Black paired with any vibrant color
        double harmonyWithBlack = _styleMatcher.CalculateColorHarmony("#000000", "#FF1493");
        harmonyWithBlack.Should().BeApproximately(0.92, 0.01);

        // White paired with any vibrant color
        double harmonyWithWhite = _styleMatcher.CalculateColorHarmony("#FFFFFF", "#32CD32");
        harmonyWithWhite.Should().BeApproximately(0.92, 0.01);
    }

    [Fact]
    public void CalculateStyleAffinity_IdenticalSilhouettes_ShouldReturnOne()
    {
        double affinity = _styleMatcher.CalculateStyleAffinity(SilhouetteTypes.LooseFit, SilhouetteTypes.LooseFit);
        affinity.Should().Be(1.0);
    }

    [Fact]
    public void CalculateStyleAffinity_LooseFitAndBoxy_ShouldReturnHighAffinity()
    {
        double affinity = _styleMatcher.CalculateStyleAffinity(SilhouetteTypes.LooseFit, SilhouetteTypes.Boxy);
        affinity.Should().Be(0.88);
    }

    [Fact]
    public void CalculateStyleAffinity_LooseFitAndSlimFit_ShouldReturnLowerAffinity()
    {
        double affinity = _styleMatcher.CalculateStyleAffinity(SilhouetteTypes.LooseFit, SilhouetteTypes.SlimFit);
        affinity.Should().Be(0.45);
    }

    [Fact]
    public void CalculateStyleAffinity_UnknownSilhouette_ShouldReturnBaselineAffinity()
    {
        double affinity = _styleMatcher.CalculateStyleAffinity("OversizedVintage", SilhouetteTypes.Standard);
        affinity.Should().Be(0.50);
    }

    #endregion

    #region 5. Multi-Objective Dynamic Shelf Merchandising Optimization

    [Fact]
    public void ComputeShelfRanking_HighMarginAndVelocity_ShouldRankAboveLowMarginAndVelocity()
    {
        var prodA = new ProductMerchandisingAttributes(
            ProductId: "hero-prod",
            Title: "Luxury Silk Blouse",
            GrossMarginPct: 0.65, // High margin (65%)
            SalesVelocityUnitsPerDay: 40.0, // High velocity
            InStockQuantity: 15, // Optimal stock
            BasePriceDzd: 18000m,
            Category: "Apparel");

        var prodB = new ProductMerchandisingAttributes(
            ProductId: "laggard-prod",
            Title: "Basic Cotton Socks",
            GrossMarginPct: 0.15, // Low margin (15%)
            SalesVelocityUnitsPerDay: 5.0, // Low velocity
            InStockQuantity: 15,
            BasePriceDzd: 800m,
            Category: "Apparel");

        var request = new ShelfRankRequest(
            Category: "Apparel",
            WeightVisual: 0.10,
            WeightMargin: 0.45,
            WeightVelocity: 0.35,
            WeightStock: 0.10);

        var result = _shelfService.ComputeShelfRanking(request, [prodA, prodB]);

        result.RankedProducts.Should().HaveCount(2);
        result.RankedProducts[0].ProductId.Should().Be("hero-prod");
        result.RankedProducts[0].ShelfRank.Should().Be(1);
        result.RankedProducts[0].FinalScore.Should().BeGreaterThan(result.RankedProducts[1].FinalScore);
        result.RankedProducts[1].ProductId.Should().Be("laggard-prod");
    }

    [Fact]
    public void ComputeShelfRanking_OutOfStockPenalization_EnsuresZeroStockDropsBelowAvailableStock()
    {
        // Product 1: In-stock with moderate margin/velocity
        var inStockProduct = new ProductMerchandisingAttributes(
            ProductId: "in-stock-item",
            Title: "In Stock Trench Coat",
            GrossMarginPct: 0.40,
            SalesVelocityUnitsPerDay: 10.0,
            InStockQuantity: 8,
            BasePriceDzd: 12000m,
            Category: "Outerwear");

        // Product 2: Out of stock (InStockQuantity = 0) with identical margin/velocity
        var outOfStockProduct = new ProductMerchandisingAttributes(
            ProductId: "oos-item",
            Title: "Out of Stock Trench Coat",
            GrossMarginPct: 0.40,
            SalesVelocityUnitsPerDay: 10.0,
            InStockQuantity: 0,
            BasePriceDzd: 12000m,
            Category: "Outerwear");

        var request = new ShelfRankRequest(
            Category: "Outerwear",
            WeightVisual: 0.20,
            WeightMargin: 0.30,
            WeightVelocity: 0.20,
            WeightStock: 0.30); // Higher stock weight to accentuate penalty

        var result = _shelfService.ComputeShelfRanking(request, [outOfStockProduct, inStockProduct]);

        result.RankedProducts.Should().HaveCount(2);
        result.RankedProducts[0].ProductId.Should().Be("in-stock-item");
        result.RankedProducts[0].StockScore.Should().Be(1.0);

        result.RankedProducts[1].ProductId.Should().Be("oos-item");
        result.RankedProducts[1].StockScore.Should().Be(0.0);
        result.RankedProducts[1].RecommendationReason.Should().Contain("Out of stock");
    }

    [Fact]
    public void ComputeShelfRanking_LowStockUrgency_AppliesPointFourMultiplier()
    {
        var product = new ProductMerchandisingAttributes(
            ProductId: "low-stock-prod",
            Title: "Scarce Cashmere Sweater",
            GrossMarginPct: 0.50,
            SalesVelocityUnitsPerDay: 12.0,
            InStockQuantity: 3, // 1 <= InStock <= 4
            BasePriceDzd: 25000m,
            Category: "Knitwear");

        var request = new ShelfRankRequest(Category: "Knitwear");

        var result = _shelfService.ComputeShelfRanking(request, [product]);

        result.RankedProducts.Should().HaveCount(1);
        result.RankedProducts[0].StockScore.Should().Be(0.4);
        result.RankedProducts[0].RecommendationReason.Should().Contain("Limited stock");
    }

    [Fact]
    public void ComputeShelfRanking_CategoryFilter_OnlyEvaluatesRequestedCategory()
    {
        var prodA = new ProductMerchandisingAttributes("a", "Shoe", 0.5, 10, 10, 5000m, "Footwear");
        var prodB = new ProductMerchandisingAttributes("b", "Dress", 0.5, 10, 10, 8000m, "Apparel");

        var request = new ShelfRankRequest(Category: "Footwear");
        var result = _shelfService.ComputeShelfRanking(request, [prodA, prodB]);

        result.RankedProducts.Should().HaveCount(1);
        result.RankedProducts[0].ProductId.Should().Be("a");
        result.TotalEvaluated.Should().Be(1);
    }

    [Fact]
    public void ComputeShelfRanking_VisualSimilarityIntegration_UsesVisualScoresWhenProvided()
    {
        var prodA = new ProductMerchandisingAttributes("vis-a", "Item A", 0.35, 10, 10, 5000m, "General");
        var prodB = new ProductMerchandisingAttributes("vis-b", "Item B", 0.35, 10, 10, 5000m, "General");

        var visualScores = new Dictionary<string, float>
        {
            ["vis-a"] = 0.95f,
            ["vis-b"] = 0.20f
        };

        var request = new ShelfRankRequest(
            WeightVisual: 0.50,
            WeightMargin: 0.20,
            WeightVelocity: 0.20,
            WeightStock: 0.10);

        var result = _shelfService.ComputeShelfRanking(request, [prodA, prodB], visualScores);

        result.RankedProducts[0].ProductId.Should().Be("vis-a");
        result.RankedProducts[0].VisualSimilarityScore.Should().Be(0.95);
        result.RankedProducts[1].ProductId.Should().Be("vis-b");
        result.RankedProducts[1].VisualSimilarityScore.Should().Be(0.20);
    }

    [Fact]
    public void ComputeShelfRanking_VisualSearchFallback_ComputesFromQueryEmbedding()
    {
        var vecTarget = CreateNormalizedVector(512, 0);
        var vecMatch = CreateNormalizedVector(512, 0); // 1.0 similarity
        var vecOther = CreateNormalizedVector(512, 1); // 0.0 similarity

        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("match-prod", "t1", "Fashion", vecMatch, [], SilhouetteTypes.Standard));
        _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord("other-prod", "t1", "Fashion", vecOther, [], SilhouetteTypes.Standard));

        var prod1 = new ProductMerchandisingAttributes("match-prod", "Matched", 0.40, 10, 10, 5000m, "Fashion");
        var prod2 = new ProductMerchandisingAttributes("other-prod", "Other", 0.40, 10, 10, 5000m, "Fashion");

        var request = new ShelfRankRequest(
            QueryEmbedding: vecTarget,
            Category: "Fashion",
            WeightVisual: 0.60,
            WeightMargin: 0.20,
            WeightVelocity: 0.10,
            WeightStock: 0.10);

        var result = _shelfService.ComputeShelfRanking(request, [prod1, prod2]);

        result.RankedProducts[0].ProductId.Should().Be("match-prod");
        result.RankedProducts[0].VisualSimilarityScore.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void ComputeShelfRanking_ExecutionTimeDiagnostics_ReturnsPositiveElapsedMs()
    {
        var prod = new ProductMerchandisingAttributes("diag-p", "Title", 0.5, 5, 5, 2000m, "Cat");
        var result = _shelfService.ComputeShelfRanking(new ShelfRankRequest(), [prod]);

        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0.0);
        result.TotalEvaluated.Should().Be(1);
    }

    [Fact]
    public void ComputeShelfRanking_EmptyProductList_ReturnsEmptyResult()
    {
        var result = _shelfService.ComputeShelfRanking(new ShelfRankRequest(), []);
        result.RankedProducts.Should().BeEmpty();
        result.TotalEvaluated.Should().Be(0);
    }

    #endregion

    #region 6. Concurrency & Thread-Safety Stress Testing

    [Fact]
    public void ConcurrentVectorIndexing_MultiThreadedWrites_MaintainsIndexIntegrityWithoutDataLoss()
    {
        const int threadCount = 8;
        const int itemsPerThread = 50;

        Parallel.For(0, threadCount, t =>
        {
            for (int i = 0; i < itemsPerThread; i++)
            {
                int id = (t * itemsPerThread) + i;
                var vec = CreateNormalizedVector(512, id % 512);
                _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord(
                    $"concurrent-{id}", "tenant-1", "Fashion", vec, [], SilhouetteTypes.Standard));
            }
        });

        _searchEngine.TotalIndexed.Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public void ConcurrentSearchAndIndex_UnderParallelWorkload_ExecutesWithoutExceptions()
    {
        // Seed initial items
        for (int i = 0; i < 20; i++)
        {
            _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord(
                $"seed-{i}", "tenant-1", "General", CreateVector(512, (float)(i + 1)), [], SilhouetteTypes.Standard));
        }

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 30, i =>
        {
            try
            {
                if (i % 2 == 0)
                {
                    // Query operation
                    var results = _searchEngine.SearchNearest(CreateVector(512, 1.0f), topK: 5);
                    results.Should().NotBeNull();
                }
                else
                {
                    // Mutation operation
                    _searchEngine.IndexProductEmbedding(new VisualEmbeddingRecord(
                        $"mut-{i}", "tenant-1", "General", CreateVector(512, 0.5f), [], SilhouetteTypes.Standard));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
    }

    #endregion

    #region 7. Dependency Injection Registration

    [Fact]
    public void AddVisualSearchServices_RegistersAllRequiredServicesAsSingletons_WithoutBuildingProvider()
    {
        // CRITICAL RULE: NEVER call services.BuildServiceProvider() in tests.
        // Assert directly on ServiceCollection descriptors.
        var services = new ServiceCollection();

        services.AddVisualSearchServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IVisualEmbeddingSearchEngine) &&
            d.ImplementationType == typeof(VisualEmbeddingSearchEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IVisualColorAndStyleMatcherService) &&
            d.ImplementationType == typeof(VisualColorAndStyleMatcherService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDynamicShelfMerchandisingService) &&
            d.ImplementationType == typeof(DynamicShelfMerchandisingService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
