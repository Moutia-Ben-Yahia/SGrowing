using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ZkIdentityVerification;

public static class ZkIdentityEndpoints
{
    public static IServiceCollection AddZkIdentityVerificationServices(this IServiceCollection services)
    {
        services.AddSingleton<ZkSelectiveAttributeDisclosureEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapZkIdentityVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/zk-identity")
            .WithTags("Autonomous Post-Quantum Zero-Knowledge Identity Verification & Selective Attribute Disclosure");

        group.MapPost("/generate-proof", (
            GenerateProofRequest request,
            ZkSelectiveAttributeDisclosureEngine engine) =>
        {
            var (_, blindingFactor) = engine.CommitAttribute(request.ActualAttributeValue);
            var proof = engine.GenerateZkRangeProof(
                request.ActualAttributeValue,
                blindingFactor,
                request.RequiredThreshold,
                request.ClaimType
            );

            return Results.Ok(proof);
        });

        group.MapPost("/verify-proof", (
            ZkRangeProof proof,
            ZkSelectiveAttributeDisclosureEngine engine) =>
        {
            var result = engine.VerifyZkRangeProof(proof);
            return Results.Ok(result);
        });

        return app;
    }
}
