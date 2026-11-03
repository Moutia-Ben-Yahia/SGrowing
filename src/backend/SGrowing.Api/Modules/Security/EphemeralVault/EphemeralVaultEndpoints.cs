using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.EphemeralVault;

public static class EphemeralVaultEndpoints
{
    public static IServiceCollection AddEphemeralVaultServices(this IServiceCollection services)
    {
        services.AddSingleton<IEphemeralVaultEngine, EphemeralVaultEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEphemeralVaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security/ephemeral-vault")
            .WithTags("Security - Ephemeral Vault & JIT Privileges");

        group.MapPost("/request-token", (RequestEphemeralTokenRequest request, IEphemeralVaultEngine engine) =>
        {
            var token = engine.IssueToken(request);
            return Results.Ok(token);
        })
        .WithName("RequestEphemeralToken")
        .WithSummary("Issues a time-bounded single-use credential with short TTL");

        group.MapGet("/active-tokens", (IEphemeralVaultEngine engine) =>
        {
            var tokens = engine.GetActiveTokens();
            return Results.Ok(tokens);
        })
        .WithName("GetActiveEphemeralTokens")
        .WithSummary("Lists active and expired ephemeral credentials with countdowns");

        group.MapPost("/revoke/{tokenId}", (string tokenId, IEphemeralVaultEngine engine) =>
        {
            var result = engine.RevokeToken(tokenId, "SecurityAdmin");
            return result.Success ? Results.Ok(result) : Results.NotFound(result);
        })
        .WithName("RevokeEphemeralToken")
        .WithSummary("Instantly invalidates single ephemeral token across distributed mesh");

        group.MapPost("/emergency-kill-all", (IEphemeralVaultEngine engine) =>
        {
            var count = engine.EmergencyRevokeAll("SecurityOfficerCiso");
            return Results.Ok(new { RevokedTokenCount = count, Status = "All Active Tokens Invalidated" });
        })
        .WithName("EmergencyKillAllEphemeralTokens")
        .WithSummary("Global kill switch: invalidates all outstanding active credentials instantly");

        return endpoints;
    }
}
