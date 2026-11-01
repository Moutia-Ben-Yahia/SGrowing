namespace SGrowing.Api.Modules.Commerce.VisualSearch;

/// <summary>
/// Canonical silhouette types for visual fashion apparel and catalog items.
/// </summary>
public static class SilhouetteTypes
{
    public const string LooseFit = "LooseFit";
    public const string SlimFit = "SlimFit";
    public const string ALine = "A-Line";
    public const string Boxy = "Boxy";
    public const string Standard = "Standard";

    public static readonly string[] All = [LooseFit, SlimFit, ALine, Boxy, Standard];
}

/// <summary>
/// Canonical commerce color names.
/// </summary>
public static class CanonicalCommerceColors
{
    public const string Noir = "Noir";
    public const string Blanc = "Blanc";
    public const string Beige = "Beige";
    public const string Bordeaux = "Bordeaux";
    public const string Olive = "Olive";
    public const string Camel = "Camel";
    public const string Navy = "Navy";
    public const string Emerald = "Emerald";
    public const string Khaki = "Khaki";

    public static readonly string[] All = [Noir, Blanc, Beige, Bordeaux, Olive, Camel, Navy, Emerald, Khaki];
}

/// <summary>
/// Represents a dominant color extracted from a product visual asset with prominence and canonical naming.
/// </summary>
public sealed record DominantColor(
    string HexCode,
    byte Red,
    byte Green,
    byte Blue,
    double ProminencePct,
    string ColorName);

/// <summary>
/// Indexed multi-dimensional neural visual embedding record for an omni-channel commerce product.
/// </summary>
public sealed record VisualEmbeddingRecord(
    string ProductId,
    string TenantId,
    string Category,
    float[] EmbeddingVector,
    DominantColor[] ColorPalette,
    string SilhouetteType,
    DateTime IndexedAtUtc)
{
    public VisualEmbeddingRecord(
        string productId,
        string tenantId,
        string category,
        float[] embeddingVector,
        DominantColor[] colorPalette,
        string silhouetteType)
        : this(productId, tenantId, category, embeddingVector, colorPalette, silhouetteType, DateTime.UtcNow)
    {
    }
}

/// <summary>
/// Ranked nearest-neighbor candidate from a neural visual vector query.
/// </summary>
public sealed record VisualSearchResult(
    string ProductId,
    float CosineSimilarity,
    int Rank,
    string Category,
    DominantColor[] DominantColors);

/// <summary>
/// Commercial, inventory, and margin attributes used by dynamic shelf optimization.
/// </summary>
public sealed record ProductMerchandisingAttributes(
    string ProductId,
    string Title,
    double GrossMarginPct,
    double SalesVelocityUnitsPerDay,
    int InStockQuantity,
    decimal BasePriceDzd,
    string Category);

/// <summary>
/// Request parameters for multi-objective digital shelf ranking.
/// </summary>
public sealed record ShelfRankRequest(
    float[]? QueryEmbedding = null,
    string? Category = null,
    int TopK = 20,
    double WeightVisual = 0.35,
    double WeightMargin = 0.30,
    double WeightVelocity = 0.25,
    double WeightStock = 0.10);

/// <summary>
/// Multi-objective scoring breakdown and resulting shelf rank for an individual product.
/// </summary>
public sealed record MerchandisedProductRank(
    string ProductId,
    int ShelfRank,
    double FinalScore,
    double VisualSimilarityScore,
    double MarginScore,
    double VelocityScore,
    double StockScore,
    string RecommendationReason);

/// <summary>
/// Aggregated digital shelf ranking output with execution diagnostics.
/// </summary>
public sealed record ShelfRankResult(
    IReadOnlyList<MerchandisedProductRank> RankedProducts,
    int TotalEvaluated,
    double ExecutionTimeMs);

/// <summary>
/// API DTO to index a product's neural visual embedding and style palette.
/// </summary>
public sealed record IndexProductVisualRequest(
    string ProductId,
    string TenantId,
    string Category,
    float[] EmbeddingVector,
    DominantColor[] ColorPalette,
    string SilhouetteType);

/// <summary>
/// API DTO to execute nearest-neighbor visual search.
/// </summary>
public sealed record VisualSearchQueryRequest(
    float[] QueryEmbedding,
    int TopK = 10,
    string? Category = null);

/// <summary>
/// API DTO to request dominant color palette extraction from raw RGB image or histogram data.
/// </summary>
public sealed record ExtractColorPaletteRequest(
    byte[] RgbHistogramData,
    int MaxColors = 5);

/// <summary>
/// API DTO returning extracted dominant colors.
/// </summary>
public sealed record ExtractColorPaletteResponse(
    bool Success,
    IReadOnlyList<DominantColor> Palette,
    string Message);

/// <summary>
/// API DTO to request visual color matching against catalog items.
/// </summary>
public sealed record ColorMatchRequest(
    string TargetHex,
    double MaxDistance = 50.0,
    string? Category = null);

/// <summary>
/// Match item result for visual color proximity.
/// </summary>
public sealed record ColorMatchItem(
    string ProductId,
    string Category,
    DominantColor MatchedColor,
    double ColorDistance,
    DominantColor[] DominantColors);

/// <summary>
/// API DTO returning color match results.
/// </summary>
public sealed record ColorMatchResponse(
    bool Success,
    IReadOnlyList<ColorMatchItem> Matches,
    string Message);

/// <summary>
/// API DTO to execute shelf ranking across a supplied candidate catalog.
/// </summary>
public sealed record ShelfRankApiRequest(
    ShelfRankRequest Request,
    List<ProductMerchandisingAttributes> Products);
