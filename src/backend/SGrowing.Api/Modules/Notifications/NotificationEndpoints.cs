using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("NotificationOrchestrator");

        group.MapPost("/dispatch", async (
            [FromBody] DispatchNotificationRequest request,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            try
            {
                var record = await service.DispatchNotificationAsync(request, ct: ct);
                return Results.Ok(record);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/receipt", async (
            [FromBody] RecordDeliveryReceiptRequest request,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            try
            {
                var record = await service.RecordDeliveryReceiptAsync(request, ct);
                return Results.Ok(record);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapPost("/sweep", async (
            [FromQuery] Guid tenantId,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            var fallenBack = await service.ProcessFallbackSweepAsync(tenantId, ct: ct);
            return Results.Ok(new { Count = fallenBack.Count, FallenBackNotifications = fallenBack });
        });

        group.MapGet("/feed/{recipientId:guid}", async (
            Guid recipientId,
            [FromQuery] Guid tenantId,
            [FromQuery] bool unreadOnly,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            var feed = await service.GetInAppFeedAsync(tenantId, recipientId, unreadOnly, ct);
            return Results.Ok(feed);
        });

        group.MapPost("/feed/{id:guid}/read", async (
            Guid id,
            [FromQuery] Guid tenantId,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            var success = await service.MarkFeedItemReadAsync(tenantId, id, ct);
            return success ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        group.MapGet("/preferences/{recipientId:guid}", async (
            Guid recipientId,
            [FromQuery] Guid tenantId,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            var prefs = await service.GetSubscriberPreferencesAsync(tenantId, recipientId, ct);
            return Results.Ok(prefs);
        });

        group.MapPut("/preferences/{recipientId:guid}", async (
            Guid recipientId,
            [FromBody] SetSubscriberPreferencesRequest request,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            try
            {
                var adjusted = request with { RecipientId = recipientId };
                var prefs = await service.SetSubscriberPreferencesAsync(adjusted, ct);
                return Results.Ok(prefs);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/policies", async (
            [FromBody] SetFallbackPolicyRequest request,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            try
            {
                var policy = await service.SetFallbackPolicyAsync(request, ct);
                return Results.Created($"/api/v1/notifications/policies/{policy.PolicyId}", policy);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/policies", async (
            [FromQuery] Guid tenantId,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            var policies = await service.GetFallbackPoliciesAsync(tenantId, ct);
            return Results.Ok(policies);
        });

        group.MapGet("/metrics", async (
            [FromQuery] Guid tenantId,
            [FromServices] INotificationOrchestratorService service,
            CancellationToken ct) =>
        {
            var metrics = await service.GetMetricsAsync(tenantId, ct);
            return Results.Ok(metrics);
        });

        return app;
    }
}
