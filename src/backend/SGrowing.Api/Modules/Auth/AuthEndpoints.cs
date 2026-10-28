using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", async (RegisterRequest request, IAuthService authService, HttpContext context) =>
        {
            try
            {
                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var response = await authService.RegisterAsync(request, ip);
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, HttpContext context) =>
        {
            try
            {
                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var response = await authService.LoginAsync(request, ip);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/refresh", async (RefreshTokenRequest request, IAuthService authService, HttpContext context) =>
        {
            try
            {
                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var response = await authService.RefreshTokenAsync(request, ip);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        });

        group.MapGet("/me", async (ClaimsPrincipal userPrincipal, IAuthService authService) =>
        {
            var userIdStr = userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? userPrincipal.FindFirst("sub")?.Value;
            var tenantIdStr = userPrincipal.FindFirst("tenant_id")?.Value;

            if (Guid.TryParse(userIdStr, out var userId) && Guid.TryParse(tenantIdStr, out var tenantId))
            {
                var profile = await authService.GetCurrentProfileSafeAsync(userId, tenantId);
                if (profile != null) return Results.Ok(profile);
            }

            return Results.Unauthorized();
        }).RequireAuthorization();

        group.MapPost("/tokens/revoke", async (
            [FromBody] RevokeTokenRequest req,
            ITokenRevocationService revocationService,
            CancellationToken ct) =>
        {
            var ttl = req.TtlSeconds.HasValue ? TimeSpan.FromSeconds(req.TtlSeconds.Value) : TimeSpan.FromDays(1);
            await revocationService.RevokeTokenAsync(req.Jti, ttl, req.Reason, ct);
            return Results.Ok(new { status = "Revoked", jti = req.Jti });
        })
        .WithName("RevokeAuthToken")
        .WithSummary("Revoke JWT token by JTI adding it to the cluster blocklist");

        group.MapGet("/tokens/{jti}/status", async (
            string jti,
            ITokenRevocationService revocationService,
            CancellationToken ct) =>
        {
            var isRevoked = await revocationService.IsTokenRevokedAsync(jti, ct);
            return Results.Ok(new { jti, isRevoked });
        })
        .WithName("CheckTokenRevocationStatus")
        .WithSummary("Check if a token JTI has been revoked");

        return endpoints;
    }

    private static async Task<UserProfileResponse?> GetCurrentProfileSafeAsync(this IAuthService authService, Guid userId, Guid tenantId)
    {
        try
        {
            return await authService.GetCurrentUserProfileAsync(userId, tenantId);
        }
        catch
        {
            return null;
        }
    }
}
