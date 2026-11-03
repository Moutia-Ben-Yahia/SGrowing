namespace SGrowing.Api.Modules.Sales.CustomerAdvocacy;

public static class CustomerAdvocacyEndpoints
{
    public static IServiceCollection AddCustomerAdvocacyServices(this IServiceCollection services)
    {
        services.AddSingleton<CustomerAdvocacyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCustomerAdvocacyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/advocacy")
            .WithTags("Sales - Customer Advocacy & Reference Matchmaking Mesh");

        group.MapGet("/overview", (CustomerAdvocacyEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/match-reference", (CustomerAdvocacyEngine engine, MatchReferenceRequest request) =>
        {
            var match = engine.MatchReference(request);
            return Results.Created($"/api/v1/sales/advocacy/matches/{match.MatchId}", match);
        });

        group.MapPost("/record-call", (CustomerAdvocacyEngine engine, RecordReferenceCallRequest request) =>
        {
            var result = engine.RecordCall(request);
            return Results.Ok(result);
        });

        return app;
    }
}
