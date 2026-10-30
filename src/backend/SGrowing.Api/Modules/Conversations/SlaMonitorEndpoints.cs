using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Conversations;

public static class SlaMonitorEndpoints
{
    public static IEndpointRouteBuilder MapSlaMonitorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/support/sla-monitor")
            .WithTags("Real-Time Predictive SLA Breach Monitor & Automated Escalation Engine");

        // GET /tickets - List monitored tickets with SLA projection and risk status
        group.MapGet("/tickets", async (
            [FromQuery] string? filter,
            IPredictiveSlaMonitorService service,
            CancellationToken ct) =>
        {
            var tickets = await service.GetMonitoredTicketsAsync(filter, ct);
            return Results.Ok(tickets);
        })
        .WithName("GetMonitoredSlaTickets")
        .WithSummary("Retrieves all open tickets monitored by the predictive SLA engine with real-time risk projections.");

        // GET /metrics - Get SLA compliance metrics and velocity gauges
        group.MapGet("/metrics", async (
            IPredictiveSlaMonitorService service,
            CancellationToken ct) =>
        {
            var metrics = await service.GetMetricsAsync(ct);
            return Results.Ok(metrics);
        })
        .WithName("GetSlaMonitorMetrics")
        .WithSummary("Returns aggregate SLA compliance metrics, breach forecasts, and active velocity.");

        // POST /evaluate - Evaluate and recalculate SLA for a single ticket
        group.MapPost("/evaluate", async (
            [FromBody] EvaluateTicketRequest request,
            IPredictiveSlaMonitorService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EvaluateTicketAsync(
                    request.TicketId,
                    request.QueueAhead,
                    request.AvgAgentVelocityMinutes,
                    request.ComplexityWeight,
                    ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("EvaluateSlaTicket")
        .WithSummary("Recalculates velocity projection and updates escalation ladder state for a specific ticket.");

        // POST /run-sweep - Execute escalation sweep across all open tickets
        group.MapPost("/run-sweep", async (
            IPredictiveSlaMonitorService service,
            CancellationToken ct) =>
        {
            var result = await service.RunEscalationSweepAsync(ct);
            return Results.Ok(result);
        })
        .WithName("RunEscalationSweep")
        .WithSummary("Executes a real-time escalation sweep across all monitored tickets and triggers automated reassignments.");

        // POST /reassign - Supervisor manual reassignment
        group.MapPost("/reassign", async (
            [FromBody] ManualReassignRequest request,
            IPredictiveSlaMonitorService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ManualReassignAsync(
                    request.TicketId,
                    request.TargetAgentId,
                    request.TargetAgentName,
                    request.Reason,
                    ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ManualReassignTicket")
        .WithSummary("Performs a supervisor manual reassignment of an escalating ticket to a dedicated specialist.");

        return app;
    }
}
