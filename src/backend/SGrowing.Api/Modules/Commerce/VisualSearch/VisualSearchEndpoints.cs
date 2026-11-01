using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.VisualSearch;

/// <summary>
/// Minimal API endpoints and Dependency Injection setup for Phase 38:
/// Autonomous Dynamic Omnichannel Merchandising &amp; Neural Visual Search Engine.
/// </summary>
public static class VisualSearchEndpoints
{
    /// <summary>
    /// Registers autonomous visual search and dynamic shelf merchandising services in DI.
    /// </summary>
    public static IServiceCollection AddVisualSearchServices(this IServiceCollection services)
    {
        services.AddSingleton<IVisualEmbeddingSearchEngine, VisualEmbeddingSearchEngine>();
        services.AddSingleton<IVisualColorAndStyleMatcherService, VisualColorAndStyleMatcherService>();
        services.AddSingleton<IDynamicShelfMerchandisingService, DynamicShelfMerchandisingService>();

        return services;
    }

    /// <summary>
    /// Maps visual search and dynamic shelf merchandising route group under /api/v1/commerce/visual-search.
    /// </summary>
    public static IEndpointRouteBuilder MapVisualSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/visual-search")
            .WithTags("Autonomous Dynamic Omnichannel Merchandising & Neural Visual Search Engine");

        // 1. POST /api/v1/commerce/visual-search/index - Index a product neural visual embedding
        group.MapPost("/index", (
            [FromBody] IndexProductVisualRequest req,
            [FromServices] IVisualEmbeddingSearchEngine engine) =>
        {
            if (string.IsNullOrWhiteSpace(req.ProductId))
            {
                return Results.BadRequest(new { Success = false, Message = "ProductId is required." });
            }

            if (req.EmbeddingVector == null || (req.EmbeddingVector.Length != 512 && req.EmbeddingVector.Length != 768))
            {
                return Results.BadRequest(new
                {
                    Success = false,
                    Message = $"EmbeddingVector must be either 512 or 768 dimensions. Provided: {req.EmbeddingVector?.Length ?? 0}."
                });
            }

            var record = new VisualEmbeddingRecord(
                ProductId: req.ProductId,
                TenantId: req.TenantId ?? "default",
                Category: req.Category ?? "General",
                EmbeddingVector: req.EmbeddingVector,
                ColorPalette: req.ColorPalette ?? [],
                SilhouetteType: req.SilhouetteType ?? SilhouetteTypes.Standard,
                IndexedAtUtc: DateTime.UtcNow);

            engine.IndexProductEmbedding(record);

            return Results.Ok(new
            {
                Success = true,
                ProductId = req.ProductId,
                TotalIndexed = engine.TotalIndexed,
                Message = "Product visual embedding indexed successfully."
            });
        })
        .WithName("IndexProductVisualEmbedding")
        .WithSummary("Indexes a 512-d or 768-d neural visual embedding for a catalog product.");

        // 2. POST /api/v1/commerce/visual-search/query - Execute nearest neighbor visual search
        group.MapPost("/query", (
            [FromBody] VisualSearchQueryRequest req,
            [FromServices] IVisualEmbeddingSearchEngine engine) =>
        {
            if (req.QueryEmbedding == null || (req.QueryEmbedding.Length != 512 && req.QueryEmbedding.Length != 768))
            {
                return Results.BadRequest(new
                {
                    Success = false,
                    Message = $"QueryEmbedding must be either 512 or 768 dimensions. Provided: {req.QueryEmbedding?.Length ?? 0}."
                });
            }

            int topK = req.TopK > 0 ? req.TopK : 10;
            var results = engine.SearchNearest(req.QueryEmbedding, topK, req.Category);

            return Results.Ok(results);
        })
        .WithName("SearchNearestVisuals")
        .WithSummary("Retrieves top-K nearest products matching query embedding via normalized cosine similarity.");

        // 3. POST /api/v1/commerce/visual-search/color-match - Match products by CIELAB color proximity
        group.MapPost("/color-match", (
            [FromBody] ColorMatchRequest req,
            [FromServices] IVisualColorAndStyleMatcherService matcher) =>
        {
            if (string.IsNullOrWhiteSpace(req.TargetHex))
            {
                return Results.BadRequest(new ColorMatchResponse(false, [], "TargetHex is required."));
            }

            double maxDistance = req.MaxDistance > 0 ? req.MaxDistance : 50.0;
            var matches = matcher.MatchByColor(req.TargetHex, maxDistance);

            if (!string.IsNullOrWhiteSpace(req.Category))
            {
                matches = matches
                    .Where(m => string.Equals(m.Category, req.Category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return Results.Ok(new ColorMatchResponse(
                Success: true,
                Matches: matches,
                Message: $"Found {matches.Count} color proximity matches for '{req.TargetHex}'."));
        })
        .WithName("MatchProductsByColor")
        .WithSummary("Finds indexed products within CIELAB color distance threshold.");

        // 4. POST /api/v1/commerce/visual-search/shelf-rank - Compute dynamic shelf ranking
        group.MapPost("/shelf-rank", (
            [FromBody] ShelfRankApiRequest req,
            [FromServices] IDynamicShelfMerchandisingService shelfService) =>
        {
            if (req.Request == null)
            {
                return Results.BadRequest(new { Success = false, Message = "Request cannot be null." });
            }

            if (req.Products == null || req.Products.Count == 0)
            {
                return Results.BadRequest(new { Success = false, Message = "Product collection cannot be null or empty." });
            }

            var result = shelfService.ComputeShelfRanking(req.Request, req.Products);
            return Results.Ok(result);
        })
        .WithName("ComputeShelfRank")
        .WithSummary("Optimizes omnichannel shelf ranking balancing visual similarity, gross margin, sales velocity, and stock availability.");

        // 5. DELETE /api/v1/commerce/visual-search/products/{productId} - Remove indexed product
        group.MapDelete("/products/{productId}", (
            string productId,
            [FromServices] IVisualEmbeddingSearchEngine engine) =>
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return Results.BadRequest(new { Success = false, Message = "ProductId is required." });
            }

            bool removed = engine.RemoveProduct(productId);
            return removed
                ? Results.Ok(new { Success = true, ProductId = productId, Message = "Product visual embedding removed from index." })
                : Results.NotFound(new { Success = false, ProductId = productId, Message = "Product embedding not found in index." });
        })
        .WithName("RemoveProductVisualEmbedding")
        .WithSummary("Removes a product neural visual embedding from in-memory search index.");

        // 6. POST /api/v1/commerce/visual-search/extract-palette - Extract dominant colors from raw histogram/pixel data
        group.MapPost("/extract-palette", (
            [FromBody] ExtractColorPaletteRequest req,
            [FromServices] IVisualColorAndStyleMatcherService matcher) =>
        {
            int maxColors = req.MaxColors > 0 ? req.MaxColors : 5;
            var palette = matcher.ExtractColorPalette(req.RgbHistogramData, maxColors);

            return Results.Ok(new ExtractColorPaletteResponse(
                Success: true,
                Palette: palette,
                Message: $"Successfully extracted {palette.Count} dominant colors."));
        })
        .WithName("ExtractVisualColorPalette")
        .WithSummary("Extracts dominant colors from raw RGB image or histogram data with canonical naming.");

        return app;
    }
}
