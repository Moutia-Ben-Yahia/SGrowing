namespace SGrowing.Api.Modules.Advertising.SponsoredBrandAds;

public static class SponsoredBrandAdsEndpoints
{
    public static IServiceCollection AddSponsoredBrandAdsServices(this IServiceCollection services)
    {
        services.AddSingleton<ISponsoredBrandAdsEngine, SponsoredBrandAdsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSponsoredBrandAdsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/sponsored-brand-ads")
            .WithTags("Advertising - Retail Media Sponsored Brand Multi-Product Banner");

        group.MapGet("/overview", (ISponsoredBrandAdsEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/create-campaign", (CreateBrandCampaignRequest req, ISponsoredBrandAdsEngine engine) =>
        {
            var campaign = engine.CreateCampaign(req.BrandName, req.Headline, req.SlotType, req.MaxCpcBidUsd, req.FeaturedSkus);
            return Results.Ok(campaign);
        });

        group.MapPost("/auction-banner", (AuctionBrandBannerRequest req, ISponsoredBrandAdsEngine engine) =>
        {
            var bid = engine.AuctionBrandBanner(req.SearchQuery, req.MinFloorCpc);
            return Results.Ok(bid);
        });

        return endpoints;
    }
}

public sealed record CreateBrandCampaignRequest(string BrandName, string Headline, BrandBannerSlotType SlotType, double MaxCpcBidUsd, List<FeaturedProductSku> FeaturedSkus);
public sealed record AuctionBrandBannerRequest(string SearchQuery, double MinFloorCpc);
