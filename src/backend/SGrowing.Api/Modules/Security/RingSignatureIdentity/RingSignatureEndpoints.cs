using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.RingSignatureIdentity;

public static class RingSignatureEndpoints
{
    public static IServiceCollection AddRingSignatureIdentityServices(this IServiceCollection services)
    {
        services.AddSingleton<IRingSignatureIdentityEngine, RingSignatureIdentityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRingSignatureIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security/ring-signature")
            .WithTags("Security - Linkable Ring Signature Identity");

        group.MapPost("/verify", (
            RingVerificationRequest request,
            IRingSignatureIdentityEngine engine) =>
        {
            var result = engine.VerifyRingSignature(request);
            return Results.Ok(result);
        })
        .WithName("VerifyRingSignatureIdentity")
        .WithSummary("Validates 1-of-N Linkable Spontaneous Anonymous Group (LSAG) ring signatures and guards against key image double spending")
        .Produces<RingVerificationResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
