using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI.Personas;

public static class AgentPersonaEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapAgentPersonaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agents/personas")
            .WithTags("Dual-Layer 38-Persona Agent Operating System");

        group.MapGet("/", async (
            [FromQuery] string? layer,
            IAgentPersonaService service,
            CancellationToken ct) =>
        {
            var personas = await service.GetPersonasAsync(layer, ct);
            return Results.Ok(personas);
        })
        .WithName("GetAgentPersonas")
        .WithSummary("List all 38 specialized agent personas across Business and Engineering layers");

        group.MapPost("/{roleKey}/execute", async (
            string roleKey,
            [FromBody] RunPersonaTaskRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAgentPersonaService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.ExecutePersonaTaskAsync(effectiveTenantId, roleKey, req, ct);
            if (result == null) return Results.NotFound(new { error = $"Agent persona '{roleKey}' not found" });
            return Results.Ok(result);
        })
        .WithName("ExecuteAgentPersonaTask")
        .WithSummary("Execute specialized task prompt using targeted agent persona");

        group.MapPost("/heartbeat/run-cycle", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAgentPersonaService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.RunHeartbeatCycleAsync(effectiveTenantId, ct);
            return Results.Ok(result);
        })
        .WithName("RunAgentHeartbeatCycle")
        .WithSummary("Execute autonomous cron heartbeat cycle auditing margins, inventory, and tasks");

        group.MapPost("/heartbeat/remediate", async (
            [FromBody] RunAgentRemediationRequest? req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAgentPersonaService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var results = await service.ExecuteAutomatedRemediationAsync(effectiveTenantId, req, ct);
            return Results.Ok(results);
        })
        .WithName("RunAgentAutomatedRemediation")
        .WithSummary("Trigger autonomous self-healing remediation actions (stock transfer manifests, margin rebalancing, task escalation)");

        group.MapGet("/heartbeat/history", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAgentPersonaService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var history = await service.GetHeartbeatHistoryAsync(effectiveTenantId, ct);
            return Results.Ok(history);
        })
        .WithName("GetAgentHeartbeatHistory")
        .WithSummary("Retrieve past heartbeat audit records and anomaly alerts");

        return app;
    }
}
