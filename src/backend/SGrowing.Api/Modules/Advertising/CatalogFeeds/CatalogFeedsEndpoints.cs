using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.CatalogFeeds;

public static class CatalogFeedsEndpoints
{
    public static IServiceCollection AddCatalogFeedsServices(this IServiceCollection services)
    {
        services.AddSingleton<ICatalogFeedsEngine, CatalogFeedsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogFeedsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/catalog-feeds")
            .WithTags("Advertising - Dynamic Product Ads (DPA) Feeds");

        group.MapGet("/", (ICatalogFeedsEngine engine) =>
        {
            var feeds = engine.GetFeeds();
            return Results.Ok(feeds);
        })
        .WithName("GetCatalogFeeds")
        .WithSummary("Retrieves syndicated omnichannel product catalog ad feeds");

        group.MapGet("/products", (ICatalogFeedsEngine engine) =>
        {
            var products = engine.GetProducts();
            return Results.Ok(products);
        })
        .WithName("GetCatalogProducts")
        .WithSummary("Retrieves products in the catalog with attached dynamic overlay badges");

        group.MapPost("/generate", (GenerateFeedRequest request, ICatalogFeedsEngine engine) =>
        {
            var feed = engine.GenerateFeed(request);
            return Results.Ok(feed);
        })
        .WithName("GenerateCatalogFeed")
        .WithSummary("Generates or refreshes a normalized ad catalog feed for a specific network");

        group.MapPost("/apply-badge", (ApplyDynamicBadgeRequest request, ICatalogFeedsEngine engine) =>
        {
            try
            {
                var updated = engine.ApplyDynamicBadge(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("ApplyProductBadge")
        .WithSummary("Applies dynamic conversion badge to catalog product across all ad feeds");

        return endpoints;
    }
}
