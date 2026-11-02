using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ZkIrisBiometrics;

public static class ZkIrisEndpoints
{
    public static IServiceCollection AddZkIrisBiometricServices(this IServiceCollection services)
    {
        services.AddSingleton<IZkIrisBiometricEngine, ZkIrisBiometricEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkIrisBiometricEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security/zk-iris")
            .WithTags("Security - Zero-Knowledge Iris Biometric Proofs");

        group.MapPost("/verify-proof", (
            ZkIrisProofRequest request,
            IZkIrisBiometricEngine engine) =>
        {
            var result = engine.VerifyProof(request);
            return Results.Ok(result);
        })
        .WithName("VerifyZkIrisBiometricProof")
        .WithSummary("Validates zero-knowledge homomorphic biometric iris code match proofs without exposing raw biometric templates")
        .Produces<ZkIrisVerificationResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
