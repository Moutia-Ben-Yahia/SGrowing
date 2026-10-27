using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI;

public record CreateAgentMissionDto(
    Guid TenantId,
    string Title,
    string TargetGoal,
    string AutonomyMode = "Human-in-the-Loop",
    decimal BudgetUsd = 50.0m
);

public record ApproveMissionStepDto(
    string ApprovedBy = "Operations Lead"
);

public static class AgentMissionEndpoints
{
    public static IEndpointRouteBuilder MapAgentMissionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agents/missions")
            .WithTags("Autonomous Agent Missions & Multi-Step MCP Loop");

        group.MapPost("", async ([FromBody] CreateAgentMissionDto req, IAutonomousAgentMissionService service, CancellationToken ct) =>
        {
            var created = await service.CreateMissionAsync(
                req.TenantId, req.Title, req.TargetGoal, req.AutonomyMode, req.BudgetUsd, ct);

            return Results.Created($"/api/agents/missions/{created.Id}", created);
        });

        group.MapGet("", async ([FromQuery] Guid tenantId, SGrowingDbContext db, CancellationToken ct) =>
        {
            var missions = await db.AgentMissions
                .Include(m => m.Steps)
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .ToListAsync(ct);

            return Results.Ok(missions);
        });

        group.MapGet("/{id:guid}", async (Guid id, [FromQuery] Guid tenantId, SGrowingDbContext db, CancellationToken ct) =>
        {
            var mission = await db.AgentMissions
                .Include(m => m.Steps)
                .FirstOrDefaultAsync(m => m.Id == id && (tenantId == Guid.Empty || m.TenantId == tenantId), ct);

            return mission != null ? Results.Ok(mission) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/steps/{stepId:guid}/approve", async (
            Guid id,
            Guid stepId,
            [FromBody] ApproveMissionStepDto req,
            IAutonomousAgentMissionService service,
            SGrowingDbContext db,
            CancellationToken ct) =>
        {
            var success = await service.ApproveMissionStepAsync(id, stepId, req.ApprovedBy, ct);
            if (!success)
            {
                return Results.NotFound(new { message = "Mission or step not found." });
            }

            await service.ExecuteMissionStepAsync(id, ct);

            var updated = await db.AgentMissions
                .Include(m => m.Steps)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            return Results.Ok(updated);
        });

        group.MapPost("/{id:guid}/pause", async (Guid id, SGrowingDbContext db, CancellationToken ct) =>
        {
            var mission = await db.AgentMissions.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (mission == null) return Results.NotFound();

            mission.Status = AgentMissionStatus.PausedForApproval;
            mission.ReasoningTrace += "\n[Manual] Mission paused by user.";
            await db.SaveChangesAsync(ct);
            return Results.Ok(mission);
        });

        group.MapPost("/{id:guid}/resume", async (Guid id, IAutonomousAgentMissionService service, SGrowingDbContext db, CancellationToken ct) =>
        {
            var mission = await db.AgentMissions.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (mission == null) return Results.NotFound();

            mission.Status = AgentMissionStatus.Running;
            mission.ReasoningTrace += "\n[Manual] Mission resumed by user.";
            await db.SaveChangesAsync(ct);

            await service.ExecuteMissionStepAsync(id, ct);
            var updated = await db.AgentMissions
                .Include(m => m.Steps)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            return Results.Ok(updated ?? mission);
        });

        group.MapPost("/{id:guid}/execute-step", async (Guid id, IAutonomousAgentMissionService service, SGrowingDbContext db, CancellationToken ct) =>
        {
            var executed = await service.ExecuteMissionStepAsync(id, ct);
            var updated = await db.AgentMissions
                .Include(m => m.Steps)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            return Results.Ok(new { executed, mission = updated });
        });

        return routes;
    }
}
