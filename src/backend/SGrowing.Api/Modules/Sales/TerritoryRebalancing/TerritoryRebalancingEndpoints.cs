using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales.TerritoryRebalancing;

public static class TerritoryRebalancingEndpoints
{
    public static IServiceCollection AddTerritoryRebalancingServices(this IServiceCollection services)
    {
        services.AddSingleton<ITerritoryRebalancingEngine, TerritoryRebalancingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTerritoryRebalancingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/territory-rebalancing")
            .WithTags("Sales - Territory Rebalancing");

        group.MapGet("/overview", (ITerritoryRebalancingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetTerritoryRebalancingOverview")
        .WithSummary("Retrieves territory distribution metrics, Gini index, and rebalancing recommendations");

        group.MapPost("/simulate", (SimulateRebalanceCommand command, ITerritoryRebalancingEngine engine) =>
        {
            var result = engine.SimulateRebalance(command);
            return Results.Ok(result);
        })
        .WithName("SimulateTerritoryRebalance")
        .WithSummary("Simulates workload equilibrium and quota improvement under a rebalance strategy");

        group.MapPost("/apply", (ApplyRebalanceCommand command, ITerritoryRebalancingEngine engine) =>
        {
            var result = engine.ApplyRebalance(command);
            return Results.Ok(result);
        })
        .WithName("ApplyTerritoryRebalance")
        .WithSummary("Schedules and executes territory rebalancing with transition grace periods");

        return app;
    }
}
