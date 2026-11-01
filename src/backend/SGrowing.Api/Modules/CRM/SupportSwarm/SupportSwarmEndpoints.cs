using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.CRM.SupportSwarm;

/// <summary>
/// Minimal API endpoints and service registration extensions for Phase 44:
/// Autonomous Omnichannel AI Support Fleet, Agentic Swarm Ticket Routing & Real-Time Escalation Radar.
/// </summary>
public static class SupportSwarmEndpoints
{
    /// <summary>
    /// Registers support swarm, CSAT radar, and autonomous macro synthesizer services into DI as Singletons.
    /// </summary>
    public static IServiceCollection AddSupportSwarmServices(this IServiceCollection services)
    {
        services.AddSingleton<IAgenticSupportSwarmRouter, AgenticSupportSwarmRouter>();
        services.AddSingleton<IInConversationCsatRadarService, InConversationCsatRadarService>();
        services.AddSingleton<IAutonomousMacroSynthesizerService, AutonomousMacroSynthesizerService>();

        return services;
    }

    /// <summary>
    /// Maps all support swarm endpoints under '/api/v1/crm/support-swarm'.
    /// </summary>
    public static IEndpointRouteBuilder MapSupportSwarmEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/support-swarm")
            .WithTags("Autonomous Omnichannel AI Support Fleet & Swarm Routing");

        // 1. POST /api/v1/crm/support-swarm/tickets/route
        group.MapPost("/tickets/route", (
            [FromBody] RouteTicketRequest request,
            [FromServices] IAgenticSupportSwarmRouter router) =>
        {
            try
            {
                var ticket = new CustomerSupportTicket(
                    TicketId: request.TicketId ?? $"TCK-{Guid.NewGuid():N}",
                    CustomerId: request.CustomerId,
                    Channel: request.Channel,
                    Category: request.Category,
                    Priority: request.Priority,
                    CustomerLtvDzd: request.CustomerLtvDzd,
                    SentimentScore: request.SentimentScore,
                    AssignedPersona: AgentPersona.LogisticsDispatcherAi, // Initial placeholder, router will assign
                    Status: TicketStatus.Open,
                    CreatedAtUtc: DateTime.UtcNow,
                    Subject: request.Subject,
                    InitialMessage: request.InitialMessage
                );

                var response = router.RouteTicket(ticket);
                return Results.Ok(response);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { error = "INVALID_PARAMETER", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("RouteCustomerSupportTicket")
        .WithSummary("Routes an incoming support ticket to an AI specialist or senior human supervisor.");

        // 2. GET /api/v1/crm/support-swarm/tickets/{ticketId}
        group.MapGet("/tickets/{ticketId}", (
            [FromRoute] string ticketId,
            [FromServices] IAgenticSupportSwarmRouter router) =>
        {
            var ticket = router.GetTicket(ticketId);
            return ticket is not null
                ? Results.Ok(ticket)
                : Results.NotFound(new { error = "TICKET_NOT_FOUND", message = $"Ticket '{ticketId}' was not found." });
        })
        .WithName("GetCustomerSupportTicket")
        .WithSummary("Retrieves a support ticket by its identifier.");

        // 3. POST /api/v1/crm/support-swarm/tickets/{ticketId}/reassign
        group.MapPost("/tickets/{ticketId}/reassign", (
            [FromRoute] string ticketId,
            [FromBody] ReassignTicketRequest request,
            [FromServices] IAgenticSupportSwarmRouter router) =>
        {
            try
            {
                var updated = router.ReassignTicket(ticketId, request.NewPersona, request.Reason);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "TICKET_NOT_FOUND", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "INVALID_STATE", message = ex.Message });
            }
        })
        .WithName("ReassignSupportTicket")
        .WithSummary("Reassigns an existing ticket to another AI persona or senior supervisor.");

        // 4. POST /api/v1/crm/support-swarm/conversations/scan
        group.MapPost("/conversations/scan", (
            [FromBody] ScanConversationRequest request,
            [FromServices] IInConversationCsatRadarService radar) =>
        {
            try
            {
                var result = radar.AnalyzeConversation(request.TicketId, request.CustomerId, request.Messages);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("ScanConversationThread")
        .WithSummary("Scans ongoing customer conversation for sentiment drop, frustration loops, and legal/fraud triggers.");

        // 5. GET /api/v1/crm/support-swarm/alerts/active
        group.MapGet("/alerts/active", (
            [FromServices] IInConversationCsatRadarService radar) =>
        {
            var alerts = radar.GetActiveAlerts();
            return Results.Ok(alerts);
        })
        .WithName("GetActiveCsatDropAlerts")
        .WithSummary("Retrieves all unacknowledged CSAT degradation and escalation radar alerts.");

        // 6. POST /api/v1/crm/support-swarm/alerts/{alertId}/acknowledge
        group.MapPost("/alerts/{alertId}/acknowledge", (
            [FromRoute] string alertId,
            [FromQuery] string? acknowledgedBy,
            [FromServices] IInConversationCsatRadarService radar) =>
        {
            try
            {
                var alert = radar.AcknowledgeAlert(alertId, acknowledgedBy);
                return Results.Ok(alert);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "ALERT_NOT_FOUND", message = ex.Message });
            }
        })
        .WithName("AcknowledgeCsatDropAlert")
        .WithSummary("Marks a CSAT degradation alert as acknowledged by supervisor.");

        // 7. POST /api/v1/crm/support-swarm/macros/synthesize
        group.MapPost("/macros/synthesize", (
            [FromBody] SynthesizeMacroRequest request,
            [FromServices] IAutonomousMacroSynthesizerService synthesizer) =>
        {
            try
            {
                var macro = synthesizer.SynthesizeMacroFromResolution(
                    request.Category,
                    request.Title,
                    request.ResolutionText,
                    request.Confidence);

                return Results.Created($"/api/v1/crm/support-swarm/macros/{macro.MacroId}", macro);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { error = "INVALID_PARAMETER", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "INVALID_REQUEST", message = ex.Message });
            }
        })
        .WithName("SynthesizeResolutionMacro")
        .WithSummary("Synthesizes a parameterized canned macro from high-satisfaction resolution text.");

        // 8. GET /api/v1/crm/support-swarm/macros/recommended
        group.MapGet("/macros/recommended", (
            [FromQuery] TicketCategory? category,
            [FromServices] IAutonomousMacroSynthesizerService synthesizer) =>
        {
            var macros = category.HasValue
                ? synthesizer.GetRecommendedMacros(category.Value)
                : synthesizer.GetAllMacros();

            return Results.Ok(macros);
        })
        .WithName("GetRecommendedMacros")
        .WithSummary("Retrieves recommended resolution macros filtered by domain category.");

        // 9. POST /api/v1/crm/support-swarm/macros/{macroId}/feedback
        group.MapPost("/macros/{macroId}/feedback", (
            [FromRoute] string macroId,
            [FromBody] RecordMacroUsageRequest request,
            [FromServices] IAutonomousMacroSynthesizerService synthesizer) =>
        {
            try
            {
                var updated = synthesizer.RecordMacroUsage(macroId, request.ResolvedIssue);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "MACRO_NOT_FOUND", message = ex.Message });
            }
        })
        .WithName("RecordMacroUsageFeedback")
        .WithSummary("Records issue resolution feedback for a macro to update its success conversion rate.");

        // 10. GET /api/v1/crm/support-swarm/summary
        group.MapGet("/summary", (
            [FromServices] IAgenticSupportSwarmRouter router,
            [FromServices] IInConversationCsatRadarService radar,
            [FromServices] IAutonomousMacroSynthesizerService synthesizer) =>
        {
            var allTickets = router.GetAllTickets();
            var activeTickets = allTickets.Count(t => t.Status is TicketStatus.InProgress or TicketStatus.Open or TicketStatus.Escalated);
            var escalatedTickets = allTickets.Count(t => t.Status == TicketStatus.Escalated);
            var activeAlerts = radar.GetActiveAlerts().Count;
            var totalMacros = synthesizer.GetAllMacros().Count;
            var workloads = router.GetAgentWorkloadSummary();

            var summary = new SupportSwarmSummaryResponse(
                TotalActiveTickets: activeTickets,
                TotalEscalatedTickets: escalatedTickets,
                ActiveAlertsCount: activeAlerts,
                TotalMacrosCount: totalMacros,
                AgentWorkloads: workloads
            );

            return Results.Ok(summary);
        })
        .WithName("GetSupportSwarmFleetSummary")
        .WithSummary("Provides real-time summary of swarm workload, active tickets, and escalation alerts.");

        return app;
    }
}
