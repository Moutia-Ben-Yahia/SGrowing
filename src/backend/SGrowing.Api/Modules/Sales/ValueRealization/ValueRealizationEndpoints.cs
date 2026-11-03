namespace SGrowing.Api.Modules.Sales.ValueRealization;

public static class ValueRealizationEndpoints
{
    public static IServiceCollection AddValueRealizationServices(this IServiceCollection services)
    {
        services.AddSingleton<ValueRealizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapValueRealizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/value-realization")
            .WithTags("Sales - B2B Value Realization & Customer Success QBR Engine");

        group.MapGet("/overview", (ValueRealizationEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/generate-qbr", (ValueRealizationEngine engine, GenerateQbrDeckRequest request) =>
        {
            var deck = engine.GenerateQbrDeck(request);
            return Results.Created($"/api/v1/sales/value-realization/decks/{deck.DeckId}", deck);
        });

        group.MapPost("/attest-roi", (ValueRealizationEngine engine, AttestRoiRequest request) =>
        {
            var metric = engine.AttestRoi(request);
            return Results.Created($"/api/v1/sales/value-realization/metrics/{metric.MetricId}", metric);
        });

        return app;
    }
}
