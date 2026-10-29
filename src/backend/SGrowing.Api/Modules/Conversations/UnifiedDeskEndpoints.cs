using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations;

public record CopilotSuggestRequest(string? LatestMessage = null);
public record AutopilotResolveRequest(string CustomerMessageText, bool ApplyResolution = true);

public static class UnifiedDeskEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapUnifiedDeskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/inbox/unified-desk")
            .WithTags("Omnichannel Unified Shared Desk & AI Autopilot");

        // 1. List conversations with channel badges & SLA countdowns
        group.MapGet("/conversations", async (
            [FromQuery] string? channel,
            [FromQuery] ConversationStatus? status,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var conversations = await deskService.GetConversationsAsync(effectiveTenantId, channel, status, ct);
            return Results.Ok(conversations);
        })
        .WithName("GetUnifiedDeskConversations")
        .WithSummary("Get omnichannel unified conversation stream with SLA countdowns and collision indicators");

        // 2. Get conversation thread, messages, Customer 360 & presence
        group.MapGet("/conversations/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var detail = await deskService.GetConversationByIdAsync(effectiveTenantId, id, ct);
            if (detail == null) return Results.NotFound(new { error = $"Conversation {id} not found." });
            return Results.Ok(detail);
        })
        .WithName("GetUnifiedDeskConversationDetail")
        .WithSummary("Get full conversation thread, internal notes, Customer 360 overview and presence");

        // 3. Send message or private internal note with soft-lock collision check
        group.MapPost("/conversations/{id:guid}/messages", async (
            Guid id,
            [FromBody] SendUnifiedDeskMessageRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await deskService.SendMessageAsync(effectiveTenantId, id, req, ct);
            if (result.SoftLockTriggered)
            {
                return Results.Conflict(result);
            }
            return Results.Ok(result);
        })
        .WithName("SendUnifiedDeskMessage")
        .WithSummary("Send customer-facing message or private internal note with soft-lock duplicate answer guard");

        // 4. Update agent presence & typing state
        group.MapPost("/conversations/{id:guid}/presence", async (
            Guid id,
            [FromBody] UpdateAgentPresenceRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var report = await deskService.UpdatePresenceAsync(effectiveTenantId, id, req, ct);
            return Results.Ok(report);
        })
        .WithName("UpdateUnifiedDeskPresence")
        .WithSummary("Heartbeat agent active viewing and typing state to prevent collision");

        // 5. Get current active viewers & typers
        group.MapGet("/conversations/{id:guid}/presence", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var report = await deskService.GetPresenceAsync(effectiveTenantId, id, ct);
            return Results.Ok(report);
        })
        .WithName("GetUnifiedDeskPresence")
        .WithSummary("Get active agent viewers, typers and collision warning for conversation");

        // 6. Generate 3 AI Copilot suggested replies grounded in customer context
        group.MapPost("/conversations/{id:guid}/copilot-suggest", async (
            Guid id,
            [FromBody] CopilotSuggestRequest? req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var suggestions = await deskService.GenerateCopilotSuggestionsAsync(effectiveTenantId, id, req?.LatestMessage, ct);
            return Results.Ok(suggestions);
        })
        .WithName("GenerateUnifiedDeskCopilotSuggestions")
        .WithSummary("Generate 3 contextual AI Copilot replies grounded in recent order status and KB articles");

        // 7. Trigger AI Autopilot resolution for transactional inquiries
        group.MapPost("/conversations/{id:guid}/autopilot-resolve", async (
            Guid id,
            [FromBody] AutopilotResolveRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var resolution = await deskService.ResolveAutopilotAsync(effectiveTenantId, id, req.CustomerMessageText, req.ApplyResolution, ct);
            return Results.Ok(resolution);
        })
        .WithName("ResolveUnifiedDeskAutopilot")
        .WithSummary("Trigger instant AI Autopilot resolution for order tracking, address updates, and returns");

        // 8. Update conversation status (handles SLA pause-on-hold)
        group.MapPost("/conversations/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateConversationStatusRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var updated = await deskService.UpdateStatusAsync(effectiveTenantId, id, req.Status, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateUnifiedDeskStatus")
        .WithSummary("Update ticket status with automatic SLA Pause-on-Hold timer support");

        // 9. Overview of active SLA countdowns & breach alerts
        group.MapGet("/sla-status", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelUnifiedDeskService deskService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var slaReport = await deskService.GetSlaOverviewAsync(effectiveTenantId, ct);
            return Results.Ok(slaReport);
        })
        .WithName("GetUnifiedDeskSlaOverview")
        .WithSummary("Get active multi-tier SLA countdowns, breach alerts, and pause-on-hold statistics");

        return app;
    }
}
