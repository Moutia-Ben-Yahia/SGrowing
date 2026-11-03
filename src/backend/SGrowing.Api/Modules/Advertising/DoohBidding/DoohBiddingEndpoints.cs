namespace SGrowing.Api.Modules.Advertising.DoohBidding;

public static class DoohBiddingEndpoints
{
    public static IServiceCollection AddDoohBiddingServices(this IServiceCollection services)
    {
        services.AddSingleton<DoohBiddingMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDoohBiddingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/dooh-bidding")
            .WithTags("Advertising - Real-Time Programmatic DOOH Bidding");

        group.MapGet("/overview", (DoohBiddingMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/submit-bid", (DoohBiddingMeshEngine engine, SubmitDoohBidRequest request) =>
        {
            var placement = engine.SubmitBid(request);
            return Results.Created($"/api/v1/advertising/dooh-bidding/placements/{placement.PlacementId}", placement);
        });

        group.MapPost("/record-foot-traffic", (DoohBiddingMeshEngine engine, RecordFootTrafficRequest request) =>
        {
            var attribution = engine.RecordFootTraffic(request);
            return Results.Created($"/api/v1/advertising/dooh-bidding/attribution/{attribution.AttributionId}", attribution);
        });

        return app;
    }
}
