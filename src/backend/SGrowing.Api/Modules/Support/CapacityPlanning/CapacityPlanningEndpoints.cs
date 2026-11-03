using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Support.CapacityPlanning;

public static class CapacityPlanningEndpoints
{
    public static IServiceCollection AddCapacityPlanningServices(this IServiceCollection services)
    {
        services.AddSingleton<ICapacityPlanningEngine, CapacityPlanningEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCapacityPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/support/capacity")
            .WithTags("Support - Workforce Capacity Planning");

        group.MapGet("/plan/{planId}", (string planId, ICapacityPlanningEngine engine) =>
        {
            var plan = engine.GetPlan(planId);
            return Results.Ok(plan);
        })
        .WithName("GetCapacityPlan")
        .WithSummary("Retrieves hourly demand forecast, staffing deficit, and Erlang-C capacity model");

        group.MapPost("/rebalance", (RebalanceCapacityRequest request, ICapacityPlanningEngine engine) =>
        {
            var updated = engine.RebalanceCapacity(request);
            return Results.Ok(updated);
        })
        .WithName("RebalanceCapacityStaffing")
        .WithSummary("Reallocates support and sales agent shift coverage to eliminate staffing deficits");

        group.MapPost("/simulate-surge", (SimulateSurgeRequest request, ICapacityPlanningEngine engine) =>
        {
            var surged = engine.SimulateSurge(request);
            return Results.Ok(surged);
        })
        .WithName("SimulateDemandSurge")
        .WithSummary("Simulates unexpected traffic surge and computes SLA degradation and deficit hours");

        return endpoints;
    }
}
