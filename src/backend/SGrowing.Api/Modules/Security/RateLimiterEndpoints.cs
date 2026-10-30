using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Security;

public record RateLimitCheckRequestDto(
    string? ClientIp,
    string? ApiKey = null,
    Guid? TenantId = null,
    string? RouteType = null,
    string? Tier = null,
    int Cost = 1,
    DateTimeOffset? SimulatedTimestamp = null
);

public record UpdatePolicyRequestDto(
    int RequestsPerMinute,
    int BurstCapacity,
    int WindowSeconds = 60
);

public record AddBlacklistRequestDto(
    string Key,
    string KeyType,
    string Reason,
    int? DurationMinutes = null,
    int BanLevel = 1
);

public record AddWhitelistRequestDto(
    string Key,
    string KeyType,
    string Reason,
    int? DurationMinutes = null
);

public record ResetKeyRequestDto(
    string Key
);

public record SetCustomOverrideRequestDto(
    string Key,
    string KeyType,
    int RequestsPerMinute,
    int BurstCapacity,
    int WindowSeconds = 60,
    string? Description = null
);

public static class RateLimiterEndpoints
{
    public static IEndpointRouteBuilder MapRateLimiterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/rate-limiter")
            .WithTags("Distributed Rate Limiting & Sliding Window Token Bucket Guard");

        // 1. POST /check - evaluate incoming request against sliding window token bucket guard
        group.MapPost("/check", (
            [FromBody] RateLimitCheckRequestDto dto,
            HttpContext httpContext,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;

            var ip = !string.IsNullOrWhiteSpace(dto.ClientIp)
                ? dto.ClientIp
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var req = new RateLimitRequest(
                ClientIp: ip,
                ApiKey: dto.ApiKey,
                TenantId: dto.TenantId,
                RouteType: dto.RouteType,
                Tier: dto.Tier,
                Cost: dto.Cost
            );

            var decision = limiter.CheckRateLimit(req, dto.SimulatedTimestamp);

            // Emit RFC-compliant HTTP Headers
            foreach (var (hKey, hVal) in decision.Headers)
            {
                httpContext.Response.Headers[hKey] = hVal;
            }

            return Results.Json(decision, statusCode: decision.StatusCode);
        })
        .WithName("RateLimiter_Check")
        .WithSummary("Evaluate request key and route against sliding window counter and token bucket guard")
        .Produces<RateLimitDecision>(StatusCodes.Status200OK)
        .Produces<RateLimitDecision>(StatusCodes.Status429TooManyRequests);

        // 2. GET /policies - list all tier policies
        group.MapGet("/policies", ([FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            var policies = limiter.GetAllPolicies();
            return Results.Ok(policies);
        })
        .WithName("RateLimiter_GetPolicies")
        .WithSummary("List all rate limiting tier policies and custom overrides");

        // 3. PUT /policies/{tier} - update rate limits and burst capacity for a tier
        group.MapPut("/policies/{tier}", (
            [FromRoute] string tier,
            [FromBody] UpdatePolicyRequestDto dto,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            if (dto.RequestsPerMinute <= 0)
            {
                return Results.BadRequest(new { error = "RequestsPerMinute must be greater than zero." });
            }

            var success = limiter.UpdateTierPolicy(tier, dto.RequestsPerMinute, dto.BurstCapacity, dto.WindowSeconds);
            if (!success)
            {
                return Results.BadRequest(new { error = $"Failed to update tier policy for '{tier}'." });
            }

            return Results.Ok(new
            {
                message = $"Policy for tier '{tier}' updated successfully.",
                tier,
                dto.RequestsPerMinute,
                dto.BurstCapacity,
                dto.WindowSeconds,
                effectiveLimit = dto.RequestsPerMinute + dto.BurstCapacity
            });
        })
        .WithName("RateLimiter_UpdatePolicy")
        .WithSummary("Update quota limit and burst capacity for a tier");

        // 4. GET /blacklist - list currently banned/blacklisted keys/IPs
        group.MapGet("/blacklist", ([FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            var list = limiter.GetBlacklist();
            return Results.Ok(list);
        })
        .WithName("RateLimiter_GetBlacklist")
        .WithSummary("List currently active blacklisted and banned client keys and IPs");

        // 5. POST /blacklist - manually blacklist an IP/ApiKey
        group.MapPost("/blacklist", (
            [FromBody] AddBlacklistRequestDto dto,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            if (string.IsNullOrWhiteSpace(dto.Key))
            {
                return Results.BadRequest(new { error = "Key cannot be empty." });
            }

            TimeSpan? duration = dto.DurationMinutes.HasValue && dto.DurationMinutes.Value > 0
                ? TimeSpan.FromMinutes(dto.DurationMinutes.Value)
                : null;

            var entry = limiter.BlacklistKey(
                key: dto.Key,
                keyType: string.IsNullOrWhiteSpace(dto.KeyType) ? "IP" : dto.KeyType,
                reason: string.IsNullOrWhiteSpace(dto.Reason) ? "Manual administrator ban" : dto.Reason,
                duration: duration,
                banLevel: dto.BanLevel > 0 ? dto.BanLevel : 1
            );

            return Results.Created($"/api/v1/security/rate-limiter/blacklist/{entry.Key}", entry);
        })
        .WithName("RateLimiter_AddBlacklist")
        .WithSummary("Manually ban or blacklist an offending IP, ApiKey, or Tenant");

        // 6. DELETE /blacklist/{key} - remove from blacklist
        group.MapDelete("/blacklist/{key}", (
            [FromRoute] string key,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            var removed = limiter.RemoveFromBlacklist(key);
            return Results.Ok(new { success = removed, key, message = removed ? "Key unbanned successfully." : "Key was not found in active blacklist." });
        })
        .WithName("RateLimiter_RemoveBlacklist")
        .WithSummary("Unban and remove an IP, ApiKey, or Tenant from the blacklist");

        // 7. GET /whitelist - list whitelisted keys
        group.MapGet("/whitelist", ([FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            var list = limiter.GetWhitelist();
            return Results.Ok(list);
        })
        .WithName("RateLimiter_GetWhitelist")
        .WithSummary("List whitelisted client keys and IPs");

        // 8. POST /whitelist - manually whitelist an IP/ApiKey
        group.MapPost("/whitelist", (
            [FromBody] AddWhitelistRequestDto dto,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            if (string.IsNullOrWhiteSpace(dto.Key))
            {
                return Results.BadRequest(new { error = "Key cannot be empty." });
            }

            TimeSpan? duration = dto.DurationMinutes.HasValue && dto.DurationMinutes.Value > 0
                ? TimeSpan.FromMinutes(dto.DurationMinutes.Value)
                : null;

            var entry = limiter.WhitelistKey(
                key: dto.Key,
                keyType: string.IsNullOrWhiteSpace(dto.KeyType) ? "IP" : dto.KeyType,
                reason: string.IsNullOrWhiteSpace(dto.Reason) ? "Manual administrator whitelist" : dto.Reason,
                duration: duration
            );

            return Results.Created($"/api/v1/security/rate-limiter/whitelist/{entry.Key}", entry);
        })
        .WithName("RateLimiter_AddWhitelist")
        .WithSummary("Whitelist an IP or ApiKey to bypass rate limiting");

        // 9. DELETE /whitelist/{key} - remove from whitelist
        group.MapDelete("/whitelist/{key}", (
            [FromRoute] string key,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            var removed = limiter.RemoveFromWhitelist(key);
            return Results.Ok(new { success = removed, key, message = removed ? "Key removed from whitelist." : "Key was not found in whitelist." });
        })
        .WithName("RateLimiter_RemoveWhitelist")
        .WithSummary("Remove a key from the whitelist");

        // 10. GET /telemetry - return rolling throughput, rejection rate %, and top throttled clients
        group.MapGet("/telemetry", ([FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            var telemetry = limiter.GetTelemetry();
            return Results.Ok(telemetry);
        })
        .WithName("RateLimiter_GetTelemetry")
        .WithSummary("Get rolling telemetry counters, rejection rate, and top throttled clients");

        // 11. POST /reset-key - reset rate limit counters for a specific key
        group.MapPost("/reset-key", (
            [FromBody] ResetKeyRequestDto dto,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            if (string.IsNullOrWhiteSpace(dto.Key))
            {
                return Results.BadRequest(new { error = "Key cannot be empty." });
            }

            var reset = limiter.ResetKey(dto.Key);
            return Results.Ok(new { success = reset, key = dto.Key, message = $"Counters and bans for key '{dto.Key}' have been reset." });
        })
        .WithName("RateLimiter_ResetKey")
        .WithSummary("Reset rate limit sliding window counter and ban status for a specific key");

        // 12. POST /custom-override - set custom policy override
        group.MapPost("/custom-override", (
            [FromBody] SetCustomOverrideRequestDto dto,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            if (string.IsNullOrWhiteSpace(dto.Key))
            {
                return Results.BadRequest(new { error = "Key cannot be empty." });
            }

            var policy = new RateLimitPolicy(
                TierName: $"Custom:{dto.Key}",
                RequestsPerMinute: dto.RequestsPerMinute,
                BurstCapacity: dto.BurstCapacity,
                WindowSeconds: dto.WindowSeconds,
                Description: dto.Description ?? $"Custom rate limit override for {dto.KeyType} '{dto.Key}'"
            );

            limiter.SetCustomOverride(dto.Key, dto.KeyType, policy);
            return Results.Ok(new { success = true, key = dto.Key, policy });
        })
        .WithName("RateLimiter_SetCustomOverride")
        .WithSummary("Set a custom rate limiting policy for a specific IP, Tenant, or ApiKey");

        // 13. DELETE /custom-override/{key} - remove custom override
        group.MapDelete("/custom-override/{key}", (
            [FromRoute] string key,
            [FromServices] IDistributedRateLimiterService? service) =>
        {
            var limiter = service ?? DistributedRateLimiterService.Instance;
            var removed = limiter.RemoveCustomOverride(key);
            return Results.Ok(new { success = removed, key, message = removed ? "Override removed." : "Override not found." });
        })
        .WithName("RateLimiter_RemoveCustomOverride")
        .WithSummary("Remove a custom rate limit override for a key");

        return app;
    }
}
