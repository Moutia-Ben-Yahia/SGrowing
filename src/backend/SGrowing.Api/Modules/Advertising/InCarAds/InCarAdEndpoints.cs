namespace SGrowing.Api.Modules.Advertising.InCarAds;

public static class InCarAdEndpoints
{
    public static IServiceCollection AddInCarAdServices(this IServiceCollection services)
    {
        services.AddSingleton<InCarAdBiddingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapInCarAdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/in-car-ads")
            .WithTags("Advertising - Connected Vehicle & In-Car Infotainment Ad Bidding");

        group.MapGet("/overview", (InCarAdBiddingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/submit-bid", (InCarAdBiddingEngine engine, SubmitInCarBidRequest request) =>
        {
            var session = engine.SubmitBid(request);
            return Results.Created($"/api/v1/advertising/in-car-ads/sessions/{session.SessionId}", session);
        });

        group.MapPost("/record-impression", (InCarAdBiddingEngine engine, RecordInCarImpressionRequest request) =>
        {
            var metric = engine.RecordImpression(request);
            return Results.Created($"/api/v1/advertising/in-car-ads/metrics/{metric.MetricId}", metric);
        });

        return app;
    }
}
