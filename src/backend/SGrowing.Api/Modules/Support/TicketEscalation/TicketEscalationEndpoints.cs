using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Support.TicketEscalation;

/// <summary>
/// Autonomous Support Ticket SLA Escalation & Multi-Tier Dispatch Desk endpoints.
/// Auto-discovered via <see cref="IEndpointModule"/>.
/// </summary>
public class TicketEscalationEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/support/tickets")
            .WithTags("Support - Autonomous SLA Escalation & Multi-Tier Dispatch Desk");

        // GET /api/v1/support/tickets/escalate
        group.MapGet("/escalate", (
            [FromQuery] Guid? tenantId,
            [FromQuery] SupportTier? tier,
            [FromQuery] EscalationState? state,
            [FromServices] ITicketEscalationService service) =>
        {
            var tid = tenantId ?? TicketEscalationService.DefaultTenantId;
            var tickets = service.GetTickets(tid, tier, state);
            return Results.Ok(tickets);
        })
        .WithName("GetEscalatedSupportTickets")
        .WithSummary("Retrieves support tickets filtered by tenant, tier, and escalation state.");

        // GET /api/v1/support/tickets/escalate/overview
        group.MapGet("/escalate/overview", (
            [FromQuery] Guid? tenantId,
            [FromServices] ITicketEscalationService service) =>
        {
            var tid = tenantId ?? TicketEscalationService.DefaultTenantId;
            var overview = service.GetOverviewReport(tid);
            return Results.Ok(overview);
        })
        .WithName("GetTicketEscalationOverviewReport")
        .WithSummary("Retrieves KPI metrics, tier distribution, SLA compliance rate, and active radar tickets.");

        // POST /api/v1/support/tickets
        group.MapPost("/", (
            [FromBody] CreateTicketRequest request,
            [FromServices] ITicketEscalationService service) =>
        {
            try
            {
                var targetTenant = request.TenantId == Guid.Empty
                    ? request with { TenantId = TicketEscalationService.DefaultTenantId }
                    : request;

                var ticket = service.CreateTicket(targetTenant);
                return Results.Created($"/api/v1/support/tickets/{ticket.TicketId}", ticket);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateSupportTicket")
        .WithSummary("Creates a new support ticket and initiates autonomous SLA monitoring.");

        // POST /api/v1/support/tickets/{id}/escalate
        group.MapPost("/{id:guid}/escalate", (
            Guid id,
            [FromBody] EscalateTicketRequest request,
            [FromServices] ITicketEscalationService service) =>
        {
            var targetRequest = request.TicketId == Guid.Empty
                ? request with { TicketId = id }
                : request;

            var targetTenant = targetRequest.TenantId == Guid.Empty
                ? targetRequest with { TenantId = TicketEscalationService.DefaultTenantId }
                : targetRequest;

            var updated = service.EscalateTicket(targetTenant);
            return updated is not null
                ? Results.Ok(updated)
                : Results.NotFound(new { error = $"Support ticket '{id}' not found or does not belong to specified tenant." });
        })
        .WithName("EscalateSupportTicket")
        .WithSummary("Escalates a support ticket to Tier 2 Technical, Tier 3 Engineering Ops, or Executive On-Call.");

        // POST /api/v1/support/tickets/{id}/resolve
        group.MapPost("/{id:guid}/resolve", (
            Guid id,
            [FromBody] ResolveTicketRequest request,
            [FromServices] ITicketEscalationService service) =>
        {
            var targetRequest = request.TicketId == Guid.Empty
                ? request with { TicketId = id }
                : request;

            var targetTenant = targetRequest.TenantId == Guid.Empty
                ? targetRequest with { TenantId = TicketEscalationService.DefaultTenantId }
                : targetRequest;

            var updated = service.ResolveTicket(targetTenant);
            return updated is not null
                ? Results.Ok(updated)
                : Results.NotFound(new { error = $"Support ticket '{id}' not found or does not belong to specified tenant." });
        })
        .WithName("ResolveSupportTicket")
        .WithSummary("Marks a support ticket resolved with audit root-cause notes and timestamps.");

        // POST /api/v1/support/tickets/{id}/suggest-macro
        group.MapPost("/{id:guid}/suggest-macro", (
            Guid id,
            [FromQuery] Guid? tenantId,
            [FromServices] ITicketEscalationService service) =>
        {
            var tid = tenantId ?? TicketEscalationService.DefaultTenantId;
            try
            {
                var suggestion = service.SuggestMacro(tid, id);
                return Results.Ok(suggestion);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("SuggestSupportTicketMacro")
        .WithSummary("Generates AI Copilot canned macro responses and action plans based on ticket sentiment and severity.");
    }
}
