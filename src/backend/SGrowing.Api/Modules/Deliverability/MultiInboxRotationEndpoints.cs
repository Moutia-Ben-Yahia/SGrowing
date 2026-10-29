using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Deliverability;

public record RegisterSenderInboxRequest(
    Guid? TenantId,
    string Email,
    string DisplayName,
    MailboxProvider Provider = MailboxProvider.GoogleWorkspace,
    int DailyQuota = 35,
    bool WarmupEnabled = true,
    int WarmupRampDailyIncrement = 3
);

public record SelectNextSenderRequest(
    string RecipientEmail,
    Guid? TenantId = null
);

public record QuarantineInboxRequest(
    Guid? TenantId,
    string? Reason
);

public record ThawInboxRequest(
    Guid? TenantId
);

public record RecordDispatchEventRequest(
    bool WasDelivered,
    bool WasBounced,
    bool WasReplied,
    bool WasOpened = false,
    bool IsSpamComplaint = false
);

public record SimulateRotationRequest(
    Guid? TenantId,
    int BatchSize = 100,
    double GoogleRatio = 0.6,
    double MicrosoftRatio = 0.3,
    double CustomRatio = 0.1
);

public static class MultiInboxRotationEndpoints
{
    public static IEndpointRouteBuilder MapMultiInboxRotationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deliverability/inboxes")
            .WithTags("Multi-Inbox Rotation & Deliverability Radar");

        // 1. GET /api/v1/deliverability/inboxes: List all pool accounts with daily quotas, sent today, and health scores
        group.MapGet("/", async (
            [FromQuery] Guid? tenantId,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? Guid.Empty;
            var accounts = await rotationService.GetPoolAccountsAsync(effectiveTenantId, ct);
            return Results.Ok(accounts);
        })
        .WithName("ListMultiInboxPoolAccounts")
        .WithSummary("List all multi-inbox rotation pool accounts for the tenant");

        // 2. POST /api/v1/deliverability/inboxes: Add sender account to pool
        group.MapPost("/", async (
            [FromBody] RegisterSenderInboxRequest req,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? Guid.NewGuid();
            var dto = new RegisterSenderInboxDto(
                Email: req.Email,
                DisplayName: req.DisplayName,
                Provider: req.Provider,
                DailyQuota: req.DailyQuota,
                WarmupEnabled: req.WarmupEnabled,
                WarmupRampDailyIncrement: req.WarmupRampDailyIncrement
            );

            var created = await rotationService.RegisterSenderAccountAsync(effectiveTenantId, dto, ct);
            return Results.Created($"/api/v1/deliverability/inboxes/{created.Id}", created);
        })
        .WithName("RegisterSenderInbox")
        .WithSummary("Register or configure a sender inbox account in the rotation pool");

        // 3. POST /api/v1/deliverability/inboxes/{id:guid}/select-next: Execute rotation selection for an outbound email
        group.MapPost("/{id:guid}/select-next", async (
            Guid id,
            [FromBody] SelectNextSenderRequest req,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            // {id} can be tenantId or pool id. If tenantId is passed in body, prefer it; otherwise use path id.
            var effectiveTenantId = req.TenantId.HasValue && req.TenantId.Value != Guid.Empty
                ? req.TenantId.Value
                : id;

            try
            {
                var selection = await rotationService.SelectNextSenderAsync(effectiveTenantId, req.RecipientEmail, ct);
                return Results.Ok(selection);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SelectNextSenderForRecipientWithId")
        .WithSummary("Execute capacity-weighted rotation with ESP-matching to select next sender inbox");

        // 3b. POST /api/v1/deliverability/inboxes/select-next: Route without {id} in path
        group.MapPost("/select-next", async (
            [FromBody] SelectNextSenderRequest req,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? Guid.Empty;
            try
            {
                var selection = await rotationService.SelectNextSenderAsync(effectiveTenantId, req.RecipientEmail, ct);
                return Results.Ok(selection);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SelectNextSenderForRecipient")
        .WithSummary("Execute capacity-weighted rotation with ESP-matching to select next sender inbox");

        // 4. POST /api/v1/deliverability/inboxes/{id:guid}/quarantine: Manually or automatically quarantine an inbox
        group.MapPost("/{id:guid}/quarantine", async (
            Guid id,
            [FromBody] QuarantineInboxRequest? req,
            [FromQuery] Guid? tenantId,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req?.TenantId ?? tenantId ?? Guid.Empty;
            try
            {
                var quarantined = await rotationService.QuarantineAccountAsync(effectiveTenantId, id, req?.Reason, ct);
                return Results.Ok(quarantined);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("QuarantineInbox")
        .WithSummary("Quarantine a sender inbox, removing it from active rotation");

        // 5. POST /api/v1/deliverability/inboxes/{id:guid}/thaw: Restore quarantined inbox to warm/active rotation
        group.MapPost("/{id:guid}/thaw", async (
            Guid id,
            [FromBody] ThawInboxRequest? req,
            [FromQuery] Guid? tenantId,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req?.TenantId ?? tenantId ?? Guid.Empty;
            try
            {
                var thawed = await rotationService.ThawAccountAsync(effectiveTenantId, id, ct);
                return Results.Ok(thawed);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ThawInbox")
        .WithSummary("Thaw a quarantined sender inbox, returning it to warm/active rotation");

        // 6. GET /api/v1/deliverability/inboxes/radar: Aggregated deliverability radar metrics for the tenant
        group.MapGet("/radar", async (
            [FromQuery] Guid? tenantId,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? Guid.Empty;
            var radar = await rotationService.GetDeliverabilityRadarAsync(effectiveTenantId, ct);
            return Results.Ok(radar);
        })
        .WithName("GetDeliverabilityRadar")
        .WithSummary("Aggregated deliverability radar metrics and health radar axes for the tenant");

        // 7. POST /api/v1/deliverability/inboxes/{id:guid}/dispatch-event: Record dispatch feedback
        group.MapPost("/{id:guid}/dispatch-event", async (
            Guid id,
            [FromBody] RecordDispatchEventRequest req,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await rotationService.RecordDispatchEventAsync(
                    senderAccountId: id,
                    wasDelivered: req.WasDelivered,
                    wasBounced: req.WasBounced,
                    wasReplied: req.WasReplied,
                    wasOpened: req.WasOpened,
                    isSpamComplaint: req.IsSpamComplaint,
                    ct: ct
                );
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("RecordInboxDispatchEvent")
        .WithSummary("Record delivery, bounce, or reply event for an inbox account");

        // 8. POST /api/v1/deliverability/inboxes/simulate: Batch rotation simulation
        group.MapPost("/simulate", async (
            [FromBody] SimulateRotationRequest req,
            IMultiInboxRotationService rotationService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? Guid.Empty;
            var result = await rotationService.SimulateBatchRotationAsync(
                tenantId: effectiveTenantId,
                batchSize: req.BatchSize,
                googleRatio: req.GoogleRatio,
                microsoftRatio: req.MicrosoftRatio,
                customRatio: req.CustomRatio,
                ct: ct
            );
            return Results.Ok(result);
        })
        .WithName("SimulateBatchRotation")
        .WithSummary("Simulate batch email distribution across the rotation pool with capacity weights and ESP matching");

        return app;
    }
}
