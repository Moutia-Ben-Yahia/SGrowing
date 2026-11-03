using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.SecretRotationMesh;

public static class SecretRotationMeshEndpoints
{
    public static IServiceCollection AddSecretRotationMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<SecretRotationMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSecretRotationMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/secret-rotation-mesh")
            .WithTags("Infrastructure - Multi-Cloud Secret Rotation & Ephemeral mTLS Mesh");

        group.MapGet("/overview", (SecretRotationMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetSecretRotationMeshOverview")
        .WithSummary("Retrieve managed secrets count, ephemeral certs issued, and HSM sync status");

        group.MapPost("/issue-ephemeral-cert", (IssueEphemeralCertRequest request, SecretRotationMeshEngine engine) =>
        {
            var result = engine.IssueEphemeralCert(request);
            return Results.Ok(result);
        })
        .WithName("IssueEphemeralMtlsCert")
        .WithSummary("Generate 15-minute ephemeral X.509 mTLS certificate signed by sovereign root CA");

        group.MapPost("/rotate-secret", (RotateSecretRequest request, SecretRotationMeshEngine engine) =>
        {
            var receipt = engine.RotateSecret(request);
            return Results.Ok(receipt);
        })
        .WithName("RotateManagedSecret")
        .WithSummary("Execute dual-credential zero-downtime secret rotation across cloud services");

        return endpoints;
    }
}
