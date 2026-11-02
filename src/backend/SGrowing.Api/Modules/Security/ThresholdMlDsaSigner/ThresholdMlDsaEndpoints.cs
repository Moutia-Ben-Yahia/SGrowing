using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.ThresholdMlDsaSigner;

public static class ThresholdMlDsaEndpoints
{
    public static IServiceCollection AddThresholdMlDsaSignerServices(this IServiceCollection services)
    {
        services.AddSingleton<IThresholdMlDsaSignerEngine, ThresholdMlDsaSignerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapThresholdMlDsaSignerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security/threshold-mldsa")
            .WithTags("Security - Post-Quantum Threshold ML-DSA Signer");

        group.MapPost("/aggregate-signature", (
            ThresholdMlDsaSigningSessionRequest request,
            IThresholdMlDsaSignerEngine engine) =>
        {
            var result = engine.AggregateAndVerifyThresholdSignature(request);
            return Results.Ok(result);
        })
        .WithName("AggregateThresholdMlDsaSignature")
        .WithSummary("Aggregates round-1 high-bit commitments and round-2 rejection-sampled response vectors into a NIST FIPS 204 lattice threshold signature")
        .Produces<ThresholdMlDsaSignatureResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
