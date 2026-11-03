using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.SpatialAdBidding;

public static class SpatialAdBiddingEndpoints
{
    public static IServiceCollection AddSpatialAdBiddingServices(this IServiceCollection services)
    {
        services.AddSingleton<SpatialAdBiddingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpatialAdBiddingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/spatial-ad-bidding")
            .WithTags("Advertising - In-Game & Metaverse 3D Spatial Ad Bidding Mesh");

        group.MapGet("/overview", (SpatialAdBiddingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetSpatialAdBiddingOverview")
        .WithSummary("Retrieve active virtual 3D placements, bid win rate, and gaze dwell metrics");

        group.MapPost("/bid-evaluation", (SpatialBidEvaluationRequest request, SpatialAdBiddingEngine engine) =>
        {
            var result = engine.EvaluateBid(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateSpatialAdBid")
        .WithSummary("Evaluate real-time 3D camera frustum, occlusion, and lighting, returning sub-30ms bid");

        group.MapPost("/verify-spatial-view", (VerifySpatialViewRequest request, SpatialAdBiddingEngine engine) =>
        {
            var attestation = engine.VerifySpatialView(request);
            return Results.Ok(attestation);
        })
        .WithName("VerifySpatialViewability")
        .WithSummary("Cryptographically attest viewer gaze dwell time and orientation angle");

        return endpoints;
    }
}
