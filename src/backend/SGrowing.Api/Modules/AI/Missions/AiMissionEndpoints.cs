using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.AI.Missions;

/// <summary>
/// Minimal API endpoints for autonomous multi-agent mission planning and task DAG execution.
/// </summary>
public class AiMissionEndpoints : IEndpointModule
{
    /// <summary>
    /// Initializes a new instance of <see cref="AiMissionEndpoints"/>.
    /// Parameterless constructor required for module auto-discovery.
    /// </summary>
    public AiMissionEndpoints()
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/missions")
            .WithTags("AI Autonomous Mission Swarm & Task DAG Engine");

        group.MapPost("/plan", (MissionObjectiveRequest request, IAiMissionPlannerService service) =>
        {
            var plan = service.PlanMission(request);
            return Results.Created($"/api/v1/ai/missions/{plan.MissionId}", plan);
        })
        .WithName("PlanAiMission")
        .WithSummary("Decomposes a strategic marketing or growth objective into an autonomous task DAG");

        group.MapGet("", (IAiMissionPlannerService service) =>
        {
            var missions = service.ListMissions();
            return Results.Ok(missions);
        })
        .WithName("ListAiMissions")
        .WithSummary("Lists all planned and active agent missions");

        group.MapGet("/{id:guid}", (Guid id, IAiMissionPlannerService service) =>
        {
            var mission = service.GetMission(id);
            return mission is not null
                ? Results.Ok(mission)
                : Results.NotFound(new { message = $"Mission with ID '{id}' was not found." });
        })
        .WithName("GetAiMission")
        .WithSummary("Retrieves current state and task DAG of a specific agent mission");

        group.MapPost("/{id:guid}/step", (Guid id, IAiMissionPlannerService service) =>
        {
            try
            {
                var plan = service.ExecuteNextStep(id);
                return Results.Ok(plan);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = $"Mission with ID '{id}' was not found." });
            }
        })
        .WithName("ExecuteAiMissionStep")
        .WithSummary("Executes the next runnable tasks in the DAG, halting at human approval gates");

        group.MapPost("/{id:guid}/approve/{taskId}", (Guid id, string taskId, IAiMissionPlannerService service) =>
        {
            try
            {
                var plan = service.ExecuteNextStep(id, taskId);
                return Results.Ok(plan);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = $"Mission with ID '{id}' was not found." });
            }
        })
        .WithName("ApproveAiMissionTask")
        .WithSummary("Grants human-in-the-loop sign-off for a gated task and continues DAG execution");
    }
}
