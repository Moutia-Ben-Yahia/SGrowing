using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Intelligence.MutualActionPlan;

public static class MutualActionPlanEndpoints
{
    public static IServiceCollection AddMutualActionPlanServices(this IServiceCollection services)
    {
        services.AddSingleton<IMutualActionPlanEngine, MutualActionPlanEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMutualActionPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/intelligence/mutual-action-plan")
            .WithTags("Intelligence - Sales Champion & Mutual Action Plan");

        group.MapPost("/create", (CreateMapRequest request, IMutualActionPlanEngine engine) =>
        {
            var plan = engine.CreatePlan(request);
            return Results.Ok(plan);
        })
        .WithName("CreateMutualActionPlan")
        .WithSummary("Creates a structured enterprise buyer mutual action plan with milestone timelines");

        group.MapGet("/{planId}", (string planId, IMutualActionPlanEngine engine) =>
        {
            var plan = engine.GetPlan(planId);
            return plan is not null ? Results.Ok(plan) : Results.NotFound();
        })
        .WithName("GetMutualActionPlan")
        .WithSummary("Retrieves a mutual action plan by plan identifier");

        group.MapGet("/deal/{dealId}", (string dealId, IMutualActionPlanEngine engine) =>
        {
            var plan = engine.GetPlanByDealId(dealId);
            return plan is not null ? Results.Ok(plan) : Results.NotFound();
        })
        .WithName("GetMutualActionPlanByDeal")
        .WithSummary("Retrieves a mutual action plan by enterprise deal identifier");

        group.MapPost("/update-milestone", (UpdateMilestoneRequest request, IMutualActionPlanEngine engine) =>
        {
            try
            {
                var updated = engine.UpdateMilestone(request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("UpdatePlanMilestone")
        .WithSummary("Updates buyer milestone execution state and evaluates schedule slippage risks");

        return endpoints;
    }
}
