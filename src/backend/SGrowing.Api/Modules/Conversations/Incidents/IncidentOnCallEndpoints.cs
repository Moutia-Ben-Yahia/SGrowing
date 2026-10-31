using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Conversations.Incidents;

public static class IncidentOnCallEndpoints
{
    public static IEndpointRouteBuilder MapIncidentOnCallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations/incidents")
            .WithTags("IncidentOnCall");

        group.MapPost("/", async (
            [FromBody] TriggerIncidentRequest request,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            try
            {
                var incident = await service.TriggerIncidentAsync(request, ct);
                return Results.Created($"/api/v1/conversations/incidents/{incident.IncidentId}", incident);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/", async (
            [FromQuery] Guid tenantId,
            [FromQuery] IncidentStatus? status,
            [FromQuery] IncidentSeverity? severity,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            var list = await service.ListIncidentsAsync(tenantId, status, severity, ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid tenantId,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            var incident = await service.GetIncidentByIdAsync(tenantId, id, ct);
            return incident != null ? Results.Ok(incident) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/ack", async (
            Guid id,
            [FromBody] AcknowledgeIncidentApiRequest request,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            try
            {
                var acked = await service.AcknowledgeIncidentAsync(request.TenantId, id, request.AgentId, request.AgentName, ct);
                return Results.Ok(acked);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id:guid}/resolve", async (
            Guid id,
            [FromBody] ResolveIncidentApiRequest request,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            try
            {
                var resolved = await service.ResolveIncidentAsync(request.TenantId, id, request.AgentId, request.ResolutionSummary, request.RootCause, ct);
                return Results.Ok(resolved);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapPost("/sweep", async (
            [FromQuery] Guid tenantId,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            var escalated = await service.EvaluatePendingEscalationsSweepAsync(tenantId, ct: ct);
            return Results.Ok(new { Count = escalated.Count, EscalatedIncidents = escalated });
        });

        group.MapGet("/metrics", async (
            [FromQuery] Guid tenantId,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            var metrics = await service.GetIncidentMetricsAsync(tenantId, ct);
            return Results.Ok(metrics);
        });

        group.MapPost("/rosters", async (
            [FromBody] CreateRosterRequest request,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            try
            {
                var roster = await service.CreateRosterAsync(request, ct);
                return Results.Created($"/api/v1/conversations/incidents/rosters/{roster.RosterId}", roster);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/rosters", async (
            [FromQuery] Guid tenantId,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            var rosters = await service.GetActiveRostersAsync(tenantId, ct);
            return Results.Ok(rosters);
        });

        group.MapPost("/policies", async (
            [FromBody] CreateEscalationPolicyRequest request,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            try
            {
                var policy = await service.CreateEscalationPolicyAsync(request, ct);
                return Results.Created($"/api/v1/conversations/incidents/policies/{policy.PolicyId}", policy);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/policies", async (
            [FromQuery] Guid tenantId,
            [FromServices] IIncidentOnCallService service,
            CancellationToken ct) =>
        {
            var policies = await service.GetPoliciesAsync(tenantId, ct);
            return Results.Ok(policies);
        });

        return app;
    }
}

public record AcknowledgeIncidentApiRequest(Guid TenantId, Guid AgentId, string AgentName);
public record ResolveIncidentApiRequest(Guid TenantId, Guid AgentId, string ResolutionSummary, string RootCause);
