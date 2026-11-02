using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.SwarmCoordination;

public static class SwarmCoordinationEndpoints
{
    public static IServiceCollection AddSwarmCoordinationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISwarmCoordinationEngine, SwarmCoordinationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSwarmCoordinationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/ai/swarm")
            .WithTags("Domain J1, J2 & J3: Autonomous Multi-Agent Swarm Collaboration Protocol");

        // 1. Create Mission
        group.MapPost("/missions", (
            [FromBody] CreateMissionRequest request,
            ISwarmCoordinationEngine engine) =>
        {
            var mission = engine.CreateMission(request);
            return Results.Created($"/api/v1/ai/swarm/missions/{mission.TenantId}/{mission.MissionId}", mission);
        })
        .WithName("CreateSwarmMission")
        .WithSummary("Decomposes a high-level growth objective into an orchestrated multi-agent task DAG.");

        // 2. Get Mission
        group.MapGet("/missions/{tenantId}/{missionId}", (
            string tenantId,
            string missionId,
            ISwarmCoordinationEngine engine) =>
        {
            var mission = engine.GetMission(tenantId, missionId);
            return mission is not null ? Results.Ok(mission) : Results.NotFound();
        })
        .WithName("GetSwarmMission")
        .WithSummary("Retrieves a mission plan, task status waterfall, and shared scratchpad memory.");

        // 3. List Missions
        group.MapGet("/missions/{tenantId}", (
            string tenantId,
            ISwarmCoordinationEngine engine) =>
        {
            var missions = engine.ListMissions(tenantId);
            return Results.Ok(missions);
        })
        .WithName("ListSwarmMissions")
        .WithSummary("Lists all missions for a tenant.");

        // 4. Approve Task (HITL)
        group.MapPost("/approve-task", (
            [FromBody] ApproveTaskRequest request,
            ISwarmCoordinationEngine engine) =>
        {
            try
            {
                var mission = engine.ApproveTask(request);
                return Results.Ok(mission);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ApproveSwarmTask")
        .WithSummary("Human-in-the-loop approval or rejection for high-risk autonomous agent actions.");

        // 5. Update Scratchpad
        group.MapPost("/scratchpad", (
            [FromBody] UpdateScratchpadRequest request,
            ISwarmCoordinationEngine engine) =>
        {
            try
            {
                var mission = engine.UpdateScratchpad(request);
                return Results.Ok(mission);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("UpdateSwarmScratchpad")
        .WithSummary("Updates working memory key-value pairs shared across the swarm.");

        // 6. Get Telemetry
        group.MapGet("/telemetry/{tenantId}/{missionId}", (
            string tenantId,
            string missionId,
            ISwarmCoordinationEngine engine) =>
        {
            try
            {
                var telemetry = engine.GetTelemetry(tenantId, missionId);
                return Results.Ok(telemetry);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GetSwarmTelemetry")
        .WithSummary("Returns swarm execution metrics, completed task counts, and pending approval items.");

        return endpoints;
    }
}
