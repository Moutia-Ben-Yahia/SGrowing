using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.SecretVaultMesh;

public static class SecretVaultMeshEndpoints
{
    public static IServiceCollection AddSecretVaultMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<ISecretVaultMeshEngine, SecretVaultMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSecretVaultMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security/secret-vault")
            .WithTags("Security - Multi-Region Secret Vault & Zeroization Mesh");

        group.MapGet("/secrets", (ISecretVaultMeshEngine engine) =>
        {
            var secrets = engine.GetSecrets();
            return Results.Ok(secrets);
        })
        .WithName("GetVaultSecrets")
        .WithSummary("Retrieves all cryptographic secrets managed across the multi-region vault mesh");

        group.MapPost("/rotate", (RotateSecretRequest request, ISecretVaultMeshEngine engine) =>
        {
            try
            {
                var rotated = engine.RotateSecret(request);
                return Results.Ok(rotated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Secret {request.SecretId} not found." });
            }
        })
        .WithName("RotateVaultSecret")
        .WithSummary("Executes cryptographic rotation and key generation for target secret");

        group.MapPost("/zeroize", (ZeroizeSecretRequest request, ISecretVaultMeshEngine engine) =>
        {
            var result = engine.ZeroizeSecret(request);
            if (!result.Success)
            {
                return Results.NotFound(result);
            }
            return Results.Ok(result);
        })
        .WithName("ZeroizeVaultSecret")
        .WithSummary("Cryptographically zeroizes and purges secret from memory across all regional clusters");

        return endpoints;
    }
}
