using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Sales.QuotaCapacity;

public static class QuotaCapacityEndpoints
{
    public static IServiceCollection AddQuotaCapacityServices(this IServiceCollection services)
    {
        services.AddSingleton<IQuotaCapacityEngine, QuotaCapacityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapQuotaCapacityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sales/quota-capacity")
            .WithTags("Sales Quota Capacity & Territory Heatmap Arbiter");

        group.MapGet("/territories", ([FromServices] IQuotaCapacityEngine engine) =>
        {
            var list = engine.GetTerritories();
            return Results.Ok(list);
        })
        .WithName("GetQuotaCapacityTerritories")
        .WithSummary("Retrieves all territory capacity and pipeline coverage profiles");

        group.MapGet("/alerts", ([FromServices] IQuotaCapacityEngine engine) =>
        {
            var alerts = engine.GetImbalanceAlerts();
            return Results.Ok(alerts);
        })
        .WithName("GetQuotaCapacityAlerts")
        .WithSummary("Retrieves burnout and whitespace capacity imbalance alerts");

        group.MapPost("/rebalance", ([FromBody] RebalanceRequest request, [FromServices] IQuotaCapacityEngine engine) =>
        {
            var result = engine.RebalanceTerritory(request);
            return Results.Ok(result);
        })
        .WithName("RebalanceTerritoryQuota")
        .WithSummary("Rebalances quota allocation and rep headcount to restore target coverage");

        return app;
    }
}
