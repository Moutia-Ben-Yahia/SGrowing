namespace SGrowing.Api.Modules.Sales.TerritoryRouting;

public static class TerritoryRoutingEndpoints
{
    public static IServiceCollection AddTerritoryRoutingServices(this IServiceCollection services)
    {
        services.AddSingleton<ITerritoryRoutingEngine, TerritoryRoutingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTerritoryRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/territories")
            .WithTags("Predictive ABM Intent Surge & Dynamic Sales Territory Routing");

        group.MapGet("/accounts", (ITerritoryRoutingEngine engine) =>
        {
            var accounts = engine.ListTargetAccounts();
            return Results.Ok(accounts);
        })
        .WithName("ListTargetAbmAccounts")
        .WithSummary("List all prioritized target accounts with multi-signal intent surge scores");

        group.MapGet("/reps", (ITerritoryRoutingEngine engine) =>
        {
            var reps = engine.ListReps();
            return Results.Ok(reps);
        })
        .WithName("ListSalesRepCapacity")
        .WithSummary("List sales rep capacity load ratios, territories, and quota attainment percentages");

        group.MapGet("/rules", (ITerritoryRoutingEngine engine) =>
        {
            var rules = engine.ListRules();
            return Results.Ok(rules);
        })
        .WithName("ListTerritoryRules")
        .WithSummary("List geographic and industry territory routing rules");

        group.MapPost("/surge", (ITerritoryRoutingEngine engine, IngestSurgeEventRequest request) =>
        {
            var account = engine.IngestSurgeSignal(request);
            return Results.Ok(account);
        })
        .WithName("IngestIntentSurgeSignal")
        .WithSummary("Ingest real-time 1st and 3rd party intent surge signals from reverse IP or content consumption");

        group.MapPost("/assign", (ITerritoryRoutingEngine engine, AssignAccountRequest request) =>
        {
            try
            {
                var result = engine.AssignAccountToOptimalRep(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("AssignAccountToRep")
        .WithSummary("Route an unassigned or surging target account to the optimal rep with capacity balancing");

        return app;
    }
}
