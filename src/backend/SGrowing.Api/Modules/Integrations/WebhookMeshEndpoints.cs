using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Integrations;

public record ToggleMeshSubscriptionRequest(bool? ForceState = null);

public record DispatchMeshEventRequest(
    string EventType,
    object Payload,
    Guid? SubscriptionId = null
);

public record ResetCircuitBreakerRequest(string? TargetUrl = null);

public record TripCircuitBreakerRequest(
    string TargetUrl,
    string? Reason = "Manually tripped by operator"
);

public record BulkDlqReplayRequest(List<Guid>? Ids = null);

public static class WebhookMeshEndpoints
{
    public static IServiceCollection AddWebhookMesh(this IServiceCollection services)
    {
        services.AddSingleton<IWebhookMeshService>(WebhookMeshService.Instance);
        return services;
    }

    private static IWebhookMeshService ResolveService(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetService<IWebhookMeshService>() ?? WebhookMeshService.Instance;
    }

    public static IEndpointRouteBuilder MapWebhookMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/integrations/webhook-mesh")
            .WithTags("Outbound Webhook Delivery Mesh & Circuit Breaker Engine");

        #region Telemetry & Live Stats

        group.MapGet("/telemetry", (HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var stats = service.GetTelemetryStats();
            return Results.Ok(stats);
        })
        .WithName("GetWebhookMeshTelemetry")
        .WithSummary("Returns live p50/p95/p99 delivery latencies, throughput, success rate, and circuit breaker metrics.");

        #endregion

        #region Subscriptions CRUD

        group.MapGet("/subscriptions", (
            [FromQuery] string? search,
            [FromQuery] bool? activeOnly,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var subs = service.ListSubscriptions(search, activeOnly);
            return Results.Ok(subs);
        })
        .WithName("ListWebhookMeshSubscriptions")
        .WithSummary("Lists registered webhook subscriptions with optional search and active status filter.");

        group.MapGet("/subscriptions/{id:guid}", (Guid id, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var sub = service.GetSubscriptionById(id);
            return sub != null ? Results.Ok(sub) : Results.NotFound(new { error = $"Subscription '{id}' not found." });
        })
        .WithName("GetWebhookMeshSubscriptionById")
        .WithSummary("Retrieves a webhook subscription by its ID.");

        group.MapPost("/subscriptions", ([FromBody] CreateMeshSubscriptionDto dto, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.TargetUrl))
            {
                return Results.BadRequest(new { error = "Name and TargetUrl are required." });
            }

            var service = ResolveService(ctx);
            var sub = service.CreateSubscription(dto);
            return Results.Created($"/api/v1/integrations/webhook-mesh/subscriptions/{sub.Id}", sub);
        })
        .WithName("CreateWebhookMeshSubscription")
        .WithSummary("Registers a new endpoint URL into the outbound delivery mesh.");

        group.MapPut("/subscriptions/{id:guid}", (Guid id, [FromBody] UpdateMeshSubscriptionDto dto, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var updated = service.UpdateSubscription(id, dto);
            return updated != null ? Results.Ok(updated) : Results.NotFound(new { error = $"Subscription '{id}' not found." });
        })
        .WithName("UpdateWebhookMeshSubscription")
        .WithSummary("Updates configuration for an existing mesh subscription.");

        group.MapDelete("/subscriptions/{id:guid}", (Guid id, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var success = service.DeleteSubscription(id);
            return success ? Results.Ok(new { message = $"Subscription '{id}' successfully removed." }) : Results.NotFound();
        })
        .WithName("DeleteWebhookMeshSubscription")
        .WithSummary("Deletes an endpoint from the webhook delivery mesh.");

        group.MapPost("/subscriptions/{id:guid}/toggle", (Guid id, [FromBody] ToggleMeshSubscriptionRequest? req, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var sub = service.ToggleActive(id, req?.ForceState);
            return sub != null ? Results.Ok(sub) : Results.NotFound();
        })
        .WithName("ToggleWebhookMeshSubscription")
        .WithSummary("Toggles active/paused delivery state for an endpoint.");

        group.MapPost("/subscriptions/{id:guid}/ping", async (Guid id, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            try
            {
                var result = await service.PingSubscriptionAsync(id);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Subscription '{id}' not found." });
            }
        })
        .WithName("PingWebhookMeshSubscription")
        .WithSummary("Dispatches a live signed test ping heartbeat to the endpoint.");

        #endregion

        #region Event Dispatch Simulation

        group.MapPost("/dispatch", async ([FromBody] DispatchMeshEventRequest req, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.EventType))
            {
                return Results.BadRequest(new { error = "EventType is required." });
            }

            var service = ResolveService(ctx);
            var results = await service.DispatchEventAsync(req.EventType, req.Payload, req.SubscriptionId);
            return Results.Ok(new
            {
                eventType = req.EventType,
                dispatchedCount = results.Count,
                successfulCount = results.Count(r => r.IsSuccess),
                failedCount = results.Count(r => !r.IsSuccess),
                results
            });
        })
        .WithName("DispatchMeshEvent")
        .WithSummary("Dispatches a webhook event across active subscriptions matching the event topic with HMAC-SHA256 signatures.");

        #endregion

        #region Circuit Breaker

        group.MapGet("/circuit-breakers", (HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var breakers = service.GetAllCircuitBreakers();
            return Results.Ok(breakers);
        })
        .WithName("ListCircuitBreakers")
        .WithSummary("Lists state, failure counts, and cooldown countdowns for all monitored endpoint circuit breakers.");

        group.MapPost("/circuit-breakers/reset", ([FromBody] ResetCircuitBreakerRequest? req, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            if (!string.IsNullOrWhiteSpace(req?.TargetUrl))
            {
                var snapshot = service.ResetCircuitBreaker(req.TargetUrl);
                return Results.Ok(new { message = $"Circuit breaker for '{req.TargetUrl}' reset to Closed.", snapshot });
            }

            var count = service.ResetAllCircuitBreakers();
            return Results.Ok(new { message = $"All {count} circuit breakers reset to Closed." });
        })
        .WithName("ResetCircuitBreaker")
        .WithSummary("Manually resets an endpoint's circuit breaker to Closed (healthy).");

        group.MapPost("/circuit-breakers/trip", ([FromBody] TripCircuitBreakerRequest req, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.TargetUrl))
            {
                return Results.BadRequest(new { error = "TargetUrl is required." });
            }

            var service = ResolveService(ctx);
            var snapshot = service.TripCircuitBreaker(req.TargetUrl, req.Reason ?? "Manually tripped by operator");
            return Results.Ok(new { message = $"Circuit breaker for '{req.TargetUrl}' tripped to Open.", snapshot });
        })
        .WithName("TripCircuitBreaker")
        .WithSummary("Manually trips a circuit breaker to Open state for testing or emergency isolation.");

        #endregion

        #region Dead Letter Queue (DLQ)

        group.MapGet("/dlq", (
            [FromQuery] string? status,
            [FromQuery] string? targetUrl,
            HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            DlqStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DlqStatus>(status, true, out var s))
            {
                parsedStatus = s;
            }

            var items = service.GetDlqItems(parsedStatus, targetUrl);
            return Results.Ok(items);
        })
        .WithName("ListDlqItems")
        .WithSummary("Lists quarantined Dead Letter Queue webhook messages with filter capabilities.");

        group.MapGet("/dlq/{id:guid}", (Guid id, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var item = service.GetDlqItemById(id);
            return item != null ? Results.Ok(item) : Results.NotFound(new { error = $"DLQ item '{id}' not found." });
        })
        .WithName("GetDlqItemById")
        .WithSummary("Retrieves detailed payload and failure error information for a single quarantined DLQ message.");

        group.MapPost("/dlq/{id:guid}/replay", async (Guid id, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var result = await service.ReplayDlqItemAsync(id);
            return Results.Ok(result);
        })
        .WithName("ReplayDlqItem")
        .WithSummary("Attempts a 1-click replay of a quarantined dead letter webhook payload.");

        group.MapPost("/dlq/replay-bulk", async ([FromBody] BulkDlqReplayRequest? req, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var result = await service.ReplayBulkDlqAsync(req?.Ids);
            return Results.Ok(result);
        })
        .WithName("ReplayBulkDlq")
        .WithSummary("Replays all or selected quarantined DLQ messages with comprehensive outcome tracking.");

        group.MapDelete("/dlq/{id:guid}", (Guid id, HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var success = service.DismissDlqItem(id);
            return success ? Results.Ok(new { message = $"DLQ item '{id}' dismissed." }) : Results.NotFound();
        })
        .WithName("DismissDlqItem")
        .WithSummary("Dismisses a single quarantined DLQ message.");

        group.MapDelete("/dlq", (HttpContext ctx) =>
        {
            var service = ResolveService(ctx);
            var dismissedCount = service.DismissAllDlq();
            return Results.Ok(new { message = $"{dismissedCount} quarantined DLQ messages dismissed.", dismissedCount });
        })
        .WithName("PurgeAllDlq")
        .WithSummary("Dismisses all quarantined dead-letter messages.");

        #endregion

        return group;
    }
}
