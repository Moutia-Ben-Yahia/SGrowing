using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.BidPacing;

public static class BidPacingEndpoints
{
    public static IServiceCollection AddBidPacingServices(this IServiceCollection services)
    {
        services.AddSingleton<IBidPacingEngine, BidPacingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBidPacingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/bid-pacing")
            .WithTags("Advertising - ABM Account Retargeting & Bid-Pacing Autopilot");

        group.MapGet("/campaigns", (IBidPacingEngine engine) =>
        {
            var campaigns = engine.GetCampaigns();
            return Results.Ok(campaigns);
        })
        .WithName("GetBidPacingCampaigns")
        .WithSummary("Retrieves all campaigns with real-time budget burn rate, pacing status, and bid multipliers");

        group.MapPost("/adjust", (AdjustBidPacingRequest request, IBidPacingEngine engine) =>
        {
            try
            {
                var result = engine.AdjustPacing(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Campaign {request.CampaignId} not found." });
            }
        })
        .WithName("AdjustCampaignBidPacing")
        .WithSummary("Adjusts bid pacing multiplier and throttling mode for target ABM campaign");

        return endpoints;
    }
}
