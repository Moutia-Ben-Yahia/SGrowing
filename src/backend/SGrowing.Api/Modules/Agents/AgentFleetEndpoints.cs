using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Agents;

public record DecomposeGoalEndpointRequest(
    string ExecutiveGoalText,
    string? TargetTimeframe = "90 Days",
    string? PrimaryDomain = null,
    Guid? TenantId = null
);

public static class AgentFleetEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? bodyTenantId, Guid? queryTenantId, Guid? headerTenantId)
    {
        if (bodyTenantId.HasValue && bodyTenantId.Value != Guid.Empty) return bodyTenantId.Value;
        if (queryTenantId.HasValue && queryTenantId.Value != Guid.Empty) return queryTenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IServiceCollection AddAgentFleetOrchestration(this IServiceCollection services)
    {
        services.AddSingleton<IAgentFleetOrchestrationService, AgentFleetOrchestrationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAgentFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agents/fleet")
            .WithTags("Dual-Layer 38-Role Autonomous Agent Workforce & Heartbeat Goal Tree");

        // 1. Roles Catalog (All 38 roles grouped into Business 17 and Engineering 21)
        group.MapGet("/roles", (IAgentFleetOrchestrationService service) =>
        {
            var catalog = service.GetRolesCatalog();
            return Results.Ok(catalog);
        })
        .WithName("GetAgentFleetRoles")
        .WithSummary("Returns all 38 specialized agent roles grouped into Business (17) and Engineering (21) with icons and competencies.");

        // 2. Goal Tree & OKR Decomposition
        group.MapPost("/goals/decompose", async (
            [FromBody] DecomposeGoalEndpointRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IAgentFleetOrchestrationService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ExecutiveGoalText))
            {
                return Results.BadRequest(new { error = "ExecutiveGoalText cannot be empty." });
            }

            var effectiveTenantId = ResolveTenantId(req.TenantId, queryTenantId, headerTenantId);
            var domainReq = new DecomposeGoalRequest(
                TenantId: effectiveTenantId,
                ExecutiveGoalText: req.ExecutiveGoalText,
                TargetTimeframe: req.TargetTimeframe ?? "90 Days",
                PrimaryDomain: req.PrimaryDomain
            );

            try
            {
                var goalTree = await service.DecomposeGoalAsync(domainReq, ct);
                return Results.Ok(goalTree);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DecomposeExecutiveGoal")
        .WithSummary("Decompose an executive high-level goal into a multi-tier mission tree with dependency DAG and agent role assignments.");

        // 3. List Active Goal Trees
        group.MapGet("/goals", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IAgentFleetOrchestrationService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(null, queryTenantId, headerTenantId);
            var goals = await service.GetGoalsAsync(effectiveTenantId, ct);
            return Results.Ok(goals);
        })
        .WithName("ListAgentFleetGoals")
        .WithSummary("List tenant active goal trees and sub-mission statuses.");

        // 4. Trigger Autonomous Heartbeat Run
        group.MapPost("/heartbeat/run", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IAgentFleetOrchestrationService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(null, queryTenantId, headerTenantId);
            var result = await service.TriggerHeartbeatAsync(effectiveTenantId, ct);
            return Results.Ok(result);
        })
        .WithName("RunAgentFleetHeartbeat")
        .WithSummary("Trigger automated heartbeat cycle across all active agent roles and advance mission tasks.");

        // 5. Activity Feed
        group.MapGet("/activity-feed", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            [FromQuery] int limit = 50,
            IAgentFleetOrchestrationService service = null!,
            CancellationToken ct = default) =>
        {
            var effectiveTenantId = ResolveTenantId(null, queryTenantId, headerTenantId);
            var feed = await service.GetActivityFeedAsync(effectiveTenantId, limit, ct);
            return Results.Ok(feed);
        })
        .WithName("GetAgentFleetActivityFeed")
        .WithSummary("Get latest autonomous actions taken by the 38-role agent workforce.");

        return app;
    }
}
