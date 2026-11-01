using System.Diagnostics;

namespace SGrowing.Api.Modules.Commerce.VisualSearch;

/// <summary>
/// Contract for multi-objective digital shelf optimization, commercial margin scoring, sales velocity weighting, and stock availability penalization.
/// </summary>
public interface IDynamicShelfMerchandisingService
{
    /// <summary>
    /// Computes multi-objective digital shelf rankings across a collection of products.
    /// </summary>
    ShelfRankResult ComputeShelfRanking(
        ShelfRankRequest request,
        IEnumerable<ProductMerchandisingAttributes> products,
        IDictionary<string, float>? visualSimilarities = null);
}

/// <summary>
/// Autonomous Dynamic Omnichannel Merchandising Engine.
/// Computes multi-objective rank:
/// FinalScore = w_vis * Sim(P, Q) + w_margin * Norm(MarginPct) + w_vel * Norm(Velocity) + w_stock * StockAvailabilityMultiplier
/// where:
/// - StockAvailabilityMultiplier = 1.0 (InStock >= 5), 0.4 (1 <= InStock <= 4), 0.0 (OutOfStock)
/// - Norm(MarginPct) scales margin (0% to 70% benchmark) to [0.0, 1.0]
/// - Norm(Velocity) scales relative to maximum observed sales velocity
/// - Visual similarity integrates query similarity or category baseline affinity
/// </summary>
public sealed class DynamicShelfMerchandisingService(IVisualEmbeddingSearchEngine searchEngine) : IDynamicShelfMerchandisingService
{
    private readonly IVisualEmbeddingSearchEngine _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));

    /// <summary>
    /// Parameterless constructor for testing and standalone instantiation.
    /// </summary>
    public DynamicShelfMerchandisingService() : this(new VisualEmbeddingSearchEngine())
    {
    }

    /// <inheritdoc/>
    public ShelfRankResult ComputeShelfRanking(
        ShelfRankRequest request,
        IEnumerable<ProductMerchandisingAttributes> products,
        IDictionary<string, float>? visualSimilarities = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(products);

        var stopwatch = Stopwatch.StartNew();

        var productList = products.ToList();
        if (productList.Count == 0)
        {
            stopwatch.Stop();
            return new ShelfRankResult([], 0, stopwatch.Elapsed.TotalMilliseconds);
        }

        // Filter by category if specified in request
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            productList = productList
                .Where(p => string.Equals(p.Category, request.Category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (productList.Count == 0)
        {
            stopwatch.Stop();
            return new ShelfRankResult([], 0, stopwatch.Elapsed.TotalMilliseconds);
        }

        // If visual similarities dictionary not supplied, attempt to compute from query embedding
        if ((visualSimilarities == null || visualSimilarities.Count == 0) &&
            request.QueryEmbedding != null &&
            request.QueryEmbedding.Length > 0)
        {
            var searchResults = _searchEngine.SearchNearest(
                request.QueryEmbedding,
                topK: Math.Max(request.TopK, productList.Count),
                categoryFilter: request.Category);

            var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in searchResults)
            {
                dict[r.ProductId] = r.CosineSimilarity;
            }
            visualSimilarities = dict;
        }

        // Weights
        double wVis = request.WeightVisual;
        double wMargin = request.WeightMargin;
        double wVel = request.WeightVelocity;
        double wStock = request.WeightStock;

        double sumWeights = wVis + wMargin + wVel + wStock;
        if (sumWeights > 0.0001)
        {
            wVis /= sumWeights;
            wMargin /= sumWeights;
            wVel /= sumWeights;
            wStock /= sumWeights;
        }
        else
        {
            wVis = 0.35;
            wMargin = 0.30;
            wVel = 0.25;
            wStock = 0.10;
        }

        // Max sales velocity for relative normalization
        double maxVelocity = 0.0;
        foreach (var p in productList)
        {
            if (p.SalesVelocityUnitsPerDay > maxVelocity)
            {
                maxVelocity = p.SalesVelocityUnitsPerDay;
            }
        }

        var scored = new List<MerchandisedProductRank>(productList.Count);

        foreach (var product in productList)
        {
            // 1. Visual Similarity Score
            double visualScore;
            if (visualSimilarities != null && visualSimilarities.TryGetValue(product.ProductId, out float sim))
            {
                visualScore = Math.Clamp(sim, 0.0, 1.0);
            }
            else if (!string.IsNullOrWhiteSpace(request.Category) &&
                     string.Equals(product.Category, request.Category, StringComparison.OrdinalIgnoreCase))
            {
                // Category baseline affinity
                visualScore = 0.50;
            }
            else
            {
                visualScore = 0.20;
            }

            // 2. Margin Normalization: Scales gross margin (e.g., 0% to 70% retail benchmark) to [0.0, 1.0]
            double grossMargin = product.GrossMarginPct;
            if (grossMargin > 1.0)
            {
                grossMargin /= 100.0; // Handle 45.0 as 0.45
            }
            double marginScore = Math.Clamp(grossMargin / 0.70, 0.0, 1.0);

            // 3. Sales Velocity Normalization: Relative to highest velocity in assortment
            double velocityScore = maxVelocity > 0.0
                ? Math.Clamp(product.SalesVelocityUnitsPerDay / maxVelocity, 0.0, 1.0)
                : 0.0;

            // 4. Stock Availability Multiplier:
            // 1.0 if InStock >= 5, 0.4 if 1 <= InStock <= 4, 0.0 if OutOfStock
            double stockMultiplier;
            if (product.InStockQuantity >= 5)
            {
                stockMultiplier = 1.0;
            }
            else if (product.InStockQuantity >= 1)
            {
                stockMultiplier = 0.4;
            }
            else
            {
                stockMultiplier = 0.0;
            }

            // Multi-objective Final Score
            double finalScore = (wVis * visualScore) +
                               (wMargin * marginScore) +
                               (wVel * velocityScore) +
                               (wStock * stockMultiplier);

            finalScore = Math.Round(finalScore, 4);

            string recommendationReason = DetermineRecommendationReason(
                product.InStockQuantity,
                visualScore,
                marginScore,
                velocityScore,
                stockMultiplier);

            scored.Add(new MerchandisedProductRank(
                ProductId: product.ProductId,
                ShelfRank: 0, // Assigned after sorting
                FinalScore: finalScore,
                VisualSimilarityScore: Math.Round(visualScore, 4),
                MarginScore: Math.Round(marginScore, 4),
                VelocityScore: Math.Round(velocityScore, 4),
                StockScore: stockMultiplier,
                RecommendationReason: recommendationReason));
        }

        // Sort by FinalScore descending, then ProductId for stable tie-breaking
        scored.Sort((a, b) =>
        {
            int cmp = b.FinalScore.CompareTo(a.FinalScore);
            return cmp != 0 ? cmp : string.Compare(a.ProductId, b.ProductId, StringComparison.Ordinal);
        });

        int topK = request.TopK > 0 ? Math.Min(request.TopK, scored.Count) : scored.Count;
        var rankedProducts = new List<MerchandisedProductRank>(topK);

        for (int i = 0; i < topK; i++)
        {
            var item = scored[i];
            rankedProducts.Add(item with { ShelfRank = i + 1 });
        }

        stopwatch.Stop();
        return new ShelfRankResult(rankedProducts, productList.Count, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static string DetermineRecommendationReason(
        int inStockQuantity,
        double visualScore,
        double marginScore,
        double velocityScore,
        double stockMultiplier)
    {
        if (inStockQuantity <= 0)
        {
            return "Out of stock - zero inventory availability penalty applied";
        }

        if (inStockQuantity < 5)
        {
            return "Limited stock remaining (low inventory alert)";
        }

        if (visualScore >= 0.75 && marginScore >= 0.65)
        {
            return "Top visual match with superior gross margin contribution";
        }

        if (visualScore >= 0.70)
        {
            return "High neural visual aesthetic match with customer query";
        }

        if (marginScore >= 0.70 && velocityScore >= 0.70)
        {
            return "High margin champion with strong sales velocity";
        }

        if (velocityScore >= 0.75)
        {
            return "High velocity bestseller with optimal stock availability";
        }

        if (marginScore >= 0.70)
        {
            return "High gross margin contributor";
        }

        return "Balanced omnichannel merchandising shelf presentation";
    }
}
