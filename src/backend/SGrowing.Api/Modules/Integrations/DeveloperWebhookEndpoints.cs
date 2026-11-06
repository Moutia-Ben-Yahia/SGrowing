using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Integrations;

public record CreateApiKeyRequest(
    Guid TenantId,
    string Name,
    List<string> Scopes,
    bool IsLive = true,
    int RateLimitPerMinute = 120
);

public record CreateSubscriptionRequest(
    Guid TenantId,
    string TargetUrl,
    List<string> Events
);

public record ToggleSubscriptionRequest(
    Guid TenantId,
    bool IsActive
);

public record TestDispatchWebhookRequest(
    Guid TenantId,
    string EventType,
    object Payload
);

public class DeveloperWebhookEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDeveloperWebhookEndpoints();
    }
}

public class ApiKeyScopeEndpointFilter : IEndpointFilter
{
    private readonly string[] _requiredScopes;

    public ApiKeyScopeEndpointFilter(params string[] requiredScopes)
    {
        _requiredScopes = requiredScopes;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        string? apiKey = null;

        if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out var headerKey) && !string.IsNullOrWhiteSpace(headerKey))
        {
            apiKey = headerKey.ToString();
        }
        else if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
                 authHeader.ToString().StartsWith("Bearer sg_", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = authHeader.ToString()["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Results.Json(new { error = "Unauthorized: Missing X-Api-Key or Bearer API token." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var service = httpContext.RequestServices.GetRequiredService<IDeveloperWebhookService>();
        var keyEntity = await service.ValidateApiKeyAsync(apiKey);

        if (keyEntity == null || keyEntity.IsRevoked)
        {
            return Results.Json(new { error = "Unauthorized: Invalid or revoked API key." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var allowedScopes = JsonSerializer.Deserialize<List<string>>(keyEntity.AllowedScopesJson) ?? new List<string>();
        if (!allowedScopes.Contains("*") && _requiredScopes.Any(s => !allowedScopes.Contains(s, StringComparer.OrdinalIgnoreCase)))
        {
            return Results.Json(new { error = $"Forbidden: API key lacks required scope(s): {string.Join(", ", _requiredScopes)}" }, statusCode: StatusCodes.Status403Forbidden);
        }

        var tenantContext = httpContext.RequestServices.GetService<ITenantContext>();
        tenantContext?.SetTenant(keyEntity.TenantId);

        return await next(context);
    }
}

public static class DeveloperWebhookEndpoints
{
    public static IEndpointRouteBuilder MapDeveloperWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/developer")
            .WithTags("Developer API & Outbound Webhooks");

        // 1. API Keys
        group.MapPost("/api-keys", async ([FromBody] CreateApiKeyRequest req, IDeveloperWebhookService service) =>
        {
            var (entity, plainKey) = await service.GenerateApiKeyAsync(req.TenantId, req.Name, req.Scopes, req.IsLive, req.RateLimitPerMinute);
            return Results.Ok(new
            {
                entity.Id,
                entity.Name,
                entity.KeyPrefix,
                entity.KeyMasked,
                plainKey, // Shown only once upon creation
                entity.AllowedScopesJson,
                entity.RateLimitPerMinute,
                entity.CreatedAtUtc
            });
        });

        group.MapGet("/api-keys", async ([FromQuery] Guid tenantId, IDeveloperWebhookService service) =>
        {
            var keys = await service.GetApiKeysAsync(tenantId);
            return Results.Ok(keys);
        });

        group.MapDelete("/api-keys/{id}", async (Guid id, [FromQuery] Guid tenantId, IDeveloperWebhookService service) =>
        {
            var success = await service.RevokeApiKeyAsync(tenantId, id);
            return success ? Results.Ok(new { message = "API key revoked" }) : Results.NotFound();
        });

        // Scoped Key Verification Ping
        group.MapGet("/v1/auth-test", () =>
        {
            return Results.Ok(new { authenticated = true, timestampUtc = DateTime.UtcNow, message = "Scoped developer API key accepted" });
        })
        .AddEndpointFilter(new ApiKeyScopeEndpointFilter("developer:read"));

        // 2. Webhook Subscriptions
        group.MapPost("/webhooks/subscriptions", async ([FromBody] CreateSubscriptionRequest req, IDeveloperWebhookService service) =>
        {
            var sub = await service.RegisterWebhookSubscriptionAsync(req.TenantId, req.TargetUrl, req.Events);
            return Results.Ok(sub);
        });

        group.MapGet("/webhooks/subscriptions", async ([FromQuery] Guid tenantId, IDeveloperWebhookService service) =>
        {
            var subs = await service.GetWebhookSubscriptionsAsync(tenantId);
            return Results.Ok(subs);
        });

        group.MapPut("/webhooks/subscriptions/{id}/toggle", async (Guid id, [FromBody] ToggleSubscriptionRequest req, IDeveloperWebhookService service) =>
        {
            var success = await service.ToggleWebhookSubscriptionAsync(req.TenantId, id, req.IsActive);
            return success ? Results.Ok(new { message = "Status updated" }) : Results.NotFound();
        });

        // 3. Webhook Delivery Logs & Test Dispatch
        group.MapGet("/webhooks/logs", async ([FromQuery] Guid tenantId, [FromQuery] Guid? subscriptionId, [FromQuery] int limit, IDeveloperWebhookService service) =>
        {
            var logs = await service.GetDeliveryLogsAsync(tenantId, subscriptionId, limit > 0 ? limit : 50);
            return Results.Ok(logs);
        });

        group.MapPost("/webhooks/test-dispatch", async ([FromBody] TestDispatchWebhookRequest req, IDeveloperWebhookService service) =>
        {
            var logs = await service.DispatchWebhookEventAsync(req.TenantId, req.EventType, req.Payload);
            return Results.Ok(new { dispatchedCount = logs.Count, logs });
        });

        group.MapPost("/webhooks/logs/{id}/retry", async (Guid id, [FromQuery] Guid tenantId, IDeveloperWebhookService service) =>
        {
            var retryLog = await service.RetryDeliveryAsync(tenantId, id);
            return retryLog != null ? Results.Ok(retryLog) : Results.NotFound();
        });

        // 4. Dead-Letter Queue (DLQ)
        group.MapGet("/webhooks/dlq", async ([FromQuery] Guid tenantId, [FromQuery] int limit, IDeveloperWebhookService service) =>
        {
            var dlq = await service.GetDeadLetterLogsAsync(tenantId, limit > 0 ? limit : 50);
            return Results.Ok(dlq);
        });

        group.MapPost("/webhooks/dlq/{id}/retry", async (Guid id, [FromQuery] Guid tenantId, IDeveloperWebhookService service) =>
        {
            var retryLog = await service.RetryDeliveryAsync(tenantId, id);
            return retryLog != null ? Results.Ok(retryLog) : Results.NotFound();
        });

        return routes;
    }
}