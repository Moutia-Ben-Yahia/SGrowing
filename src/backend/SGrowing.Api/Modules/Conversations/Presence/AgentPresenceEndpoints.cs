using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Conversations.Presence;

public record AutoHandoffSweepApiRequest(
    double? AbandonmentThresholdMinutes = null
);

public static class AgentPresenceEndpoints
{
    public static IEndpointRouteBuilder MapAgentPresenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations/presence")
            .WithTags("Agent Presence & Collision Prevention Mesh");

        // 1. POST /heartbeat: Send agent presence heartbeat with current ticket context
        group.MapPost("/heartbeat", async (AgentHeartbeatRequest request, IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var session = await presenceService.PingHeartbeatAsync(request, null, ct);
            return Results.Ok(session);
        })
        .WithName("PingAgentPresenceHeartbeat")
        .WithSummary("Send agent presence heartbeat with active ticket context");

        // 2. POST /tickets/{ticketId:guid}/lock: Acquire composing collision lock on ticket
        group.MapPost("/tickets/{ticketId:guid}/lock", async (Guid ticketId, AcquireLockRequest request, IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var result = await presenceService.AcquireCollisionLockAsync(ticketId, request, null, ct);
            return Results.Ok(result);
        })
        .WithName("AcquireTicketCollisionLock")
        .WithSummary("Acquire or renew composing collision lock on an omnichannel ticket");

        // 3. POST /tickets/{ticketId:guid}/unlock: Release collision lock on ticket
        group.MapPost("/tickets/{ticketId:guid}/unlock", async (Guid ticketId, ReleaseLockRequest request, IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var result = await presenceService.ReleaseCollisionLockAsync(ticketId, request, null, ct);
            return Results.Ok(result);
        })
        .WithName("ReleaseTicketCollisionLock")
        .WithSummary("Release collision lock on an omnichannel ticket");

        // 4. GET /tickets/{ticketId:guid}/viewers: Get active viewers and lock holder for ticket
        group.MapGet("/tickets/{ticketId:guid}/viewers", async (Guid ticketId, IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var viewers = await presenceService.GetTicketViewersAsync(ticketId, null, ct);
            return Results.Ok(viewers);
        })
        .WithName("GetTicketViewers")
        .WithSummary("Get list of active operators viewing a ticket");

        // 4b. GET /tickets/{ticketId:guid}/status: Get full collision status (lock, viewers, typing beacon)
        group.MapGet("/tickets/{ticketId:guid}/status", async (Guid ticketId, Guid? agentId, IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var status = await presenceService.GetTicketCollisionStatusAsync(ticketId, agentId, null, ct);
            return Results.Ok(status);
        })
        .WithName("GetTicketCollisionStatus")
        .WithSummary("Get real-time collision status, shield warning, and typing beacon for a ticket");

        // 5. GET /team-status: Get live roster of all agents, presence states, and active workloads
        group.MapGet("/team-status", async (IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var roster = await presenceService.GetTeamStatusRosterAsync(null, ct);
            return Results.Ok(roster);
        })
        .WithName("GetTeamPresenceStatus")
        .WithSummary("Get live roster of all operators, presence states, and active workloads");

        // 6. POST /auto-handoff/sweep: Trigger evaluation sweep for abandoned tickets and execute auto-reassignment
        group.MapPost("/auto-handoff/sweep", async (AutoHandoffSweepApiRequest? request, IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            TimeSpan? threshold = request?.AbandonmentThresholdMinutes.HasValue == true
                ? TimeSpan.FromMinutes(request.AbandonmentThresholdMinutes.Value)
                : null;

            var result = await presenceService.ExecuteAutoHandoffSweepAsync(threshold, null, ct);
            return Results.Ok(result);
        })
        .WithName("TriggerAutoHandoffSweep")
        .WithSummary("Trigger evaluation sweep for abandoned tickets and reassign to lowest workload operators");

        // 7. GET /metrics: Presence and collision prevention statistics
        group.MapGet("/metrics", async (IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var metrics = await presenceService.GetMetricsAsync(null, ct);
            return Results.Ok(metrics);
        })
        .WithName("GetPresenceCollisionMetrics")
        .WithSummary("Get omnichannel presence telemetry and collision prevention statistics");

        // 8. GET /tickets: List all tickets in the presence mesh
        group.MapGet("/tickets", async (IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var tickets = await presenceService.GetAllTicketsAsync(ct);
            return Results.Ok(tickets);
        })
        .WithName("GetAllPresenceTickets")
        .WithSummary("List all tickets managed within the real-time presence mesh");

        // 9. GET /auto-handoff/audit: List handoff audit events
        group.MapGet("/auto-handoff/audit", async (int? limit, IAgentPresenceCollisionService presenceService, CancellationToken ct) =>
        {
            var events = await presenceService.GetHandoffAuditEventsAsync(limit ?? 50, ct);
            return Results.Ok(events);
        })
        .WithName("GetAutoHandoffAuditLog")
        .WithSummary("Get chronological audit log of automated ticket handoffs");

        // 10. POST /reset: Reset presence mesh to initial seeded state
        group.MapPost("/reset", (IAgentPresenceCollisionService presenceService) =>
        {
            presenceService.ResetToSeedData();
            return Results.Ok(new { message = "Presence and collision state reset to initial seed." });
        })
        .WithName("ResetPresenceMesh")
        .WithSummary("Reset presence mesh to default seed data");

        return app;
    }
}
