using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Agents.GrowthDirector;

public static class GrowthDirectorEndpoints
{
    public static IServiceCollection AddGrowthDirectorServices(this IServiceCollection services)
    {
        services.AddSingleton<IGrowthDirectorEngine, GrowthDirectorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGrowthDirectorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/agents/growth-director")
            .WithTags("Supervised Autonomous AI Growth Director (Agent Mission Loop)");

        // 1. Decompose Objective
        group.MapPost("/decompose", ([FromBody] DecomposeObjectiveRequest request, IGrowthDirectorEngine engine) =>
        {
            var objective = request.ResolveObjective();
            if (objective is null)
            {
                return Results.BadRequest(new { error = "A valid GrowthMissionObjective or Title is required." });
            }

            var plan = engine.DecomposeObjective(objective, request.Policy);
            return Results.Ok(plan);
        })
        .WithName("DecomposeGrowthObjective")
        .WithSummary("Decomposes a growth objective into coordinated specialist agent sub-tasks with financial guardrails.");

        // 2. Approve and Execute Mission
        group.MapPost("/approve-and-execute", ([FromBody] ApproveAndExecuteRequest request, IGrowthDirectorEngine engine) =>
        {
            if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.MissionId))
            {
                return Results.BadRequest(new { error = "TenantId and MissionId are required." });
            }

            var result = engine.ApproveAndExecuteMission(
                request.TenantId,
                request.MissionId,
                request.ApprovedTaskIds ?? new List<string>()
            );

            return result.Success ? Results.Ok(result) : Results.NotFound(result);
        })
        .WithName("ApproveAndExecuteGrowthMission")
        .WithSummary("Executes approved mission sub-tasks, enforcing financial guardrails and human approval decisions.");

        // 3. Get Mission
        group.MapGet("/missions/{tenantId}/{missionId}", (string tenantId, string missionId, IGrowthDirectorEngine engine) =>
        {
            var plan = engine.GetMission(tenantId, missionId);
            return plan is not null
                ? Results.Ok(plan)
                : Results.NotFound(new { error = $"Growth mission '{missionId}' not found for tenant '{tenantId}'." });
        })
        .WithName("GetGrowthMission")
        .WithSummary("Retrieves the current execution plan and status for a growth mission.");

        return endpoints;
    }
}
