namespace SGrowing.Api.Modules.Advertising.SignageSsp;

public static class SignageSspEndpoints
{
    public static IServiceCollection AddSignageSspServices(this IServiceCollection services)
    {
        services.AddSingleton<ISignageSspEngine, SignageSspEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSignageSspEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/signage-ssp")
            .WithTags("Advertising - In-Store Digital Signage SSP");

        group.MapGet("/overview", (ISignageSspEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/register-node", (RegisterDisplayNodeRequest req, ISignageSspEngine engine) =>
        {
            var node = engine.RegisterDisplayNode(req.VenueName, req.LocationType, req.BaseFloorCpm);
            return Results.Ok(node);
        });

        group.MapPost("/auction-slot", (AuctionSlotRequest req, ISignageSspEngine engine) =>
        {
            var auction = engine.AuctionSlot(req.NodeId, req.DurationSeconds, req.MinBidThreshold);
            return Results.Ok(auction);
        });

        group.MapPost("/verify-play", (VerifyPlayRequest req, ISignageSspEngine engine) =>
        {
            var attestation = engine.VerifyProofOfPlay(req.AuctionId, req.NodeId, req.DurationSeconds, req.AudienceImpressions);
            return Results.Ok(attestation);
        });

        group.MapPost("/settle-revenue", (SettleRevenueRequest req, ISignageSspEngine engine) =>
        {
            var settlement = engine.SettleRevenue(req.VenueName, req.GrossRevenue);
            return Results.Ok(settlement);
        });

        return endpoints;
    }
}

public sealed record RegisterDisplayNodeRequest(string VenueName, ScreenLocationType LocationType, double BaseFloorCpm);
public sealed record AuctionSlotRequest(string NodeId, int DurationSeconds, double MinBidThreshold);
public sealed record VerifyPlayRequest(string AuctionId, string NodeId, int DurationSeconds, int AudienceImpressions);
public sealed record SettleRevenueRequest(string VenueName, double GrossRevenue);
