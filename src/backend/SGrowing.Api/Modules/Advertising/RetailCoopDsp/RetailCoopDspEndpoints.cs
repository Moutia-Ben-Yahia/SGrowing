using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.RetailCoopDsp;

public static class RetailCoopDspEndpoints
{
    public static IServiceCollection AddRetailCoopDspServices(this IServiceCollection services)
    {
        services.AddSingleton<RetailCoopDspEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRetailCoopDspEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/retail-coop-dsp")
            .WithTags("Advertising - Retail Media Off-Site Co-Op DSP & Clean Rooms");

        group.MapGet("/overview", (RetailCoopDspEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetRetailCoopDspOverview")
        .WithSummary("Retrieve connected retailer clean rooms, active co-op ad campaigns, and closed-loop ROAS");

        group.MapPost("/launch-campaign", (LaunchCoopCampaignRequest request, RetailCoopDspEngine engine) =>
        {
            var campaign = engine.LaunchCoopCampaign(request);
            return Results.Ok(campaign);
        })
        .WithName("LaunchRetailCoopCampaign")
        .WithSummary("Launch off-site programmatic co-op campaign powered by retailer clean room audience segment");

        group.MapPost("/cleanroom-match", (ExecuteCleanRoomMatchRequest request, RetailCoopDspEngine engine) =>
        {
            var receipt = engine.ExecuteCleanRoomMatch(request);
            return Results.Ok(receipt);
        })
        .WithName("ExecuteCleanRoomMatch")
        .WithSummary("Cryptographically match first-party hashed identities with retailer purchase graphs");

        return endpoints;
    }
}
