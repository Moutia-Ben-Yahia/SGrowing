using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.SpacecraftAerocaptureGuidance;

public static class AerocaptureGuidanceEndpoints
{
    public static IServiceCollection AddSpacecraftAerocaptureGuidanceServices(this IServiceCollection services)
    {
        services.AddSingleton<ISpacecraftAerocaptureGuidanceEngine, SpacecraftAerocaptureGuidanceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpacecraftAerocaptureGuidanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/aerocapture")
            .WithTags("Aerospace - Spacecraft Aerocapture Hypersonic Guidance");

        group.MapPost("/compute-guidance", (
            AerocaptureGuidanceRequest request,
            ISpacecraftAerocaptureGuidanceEngine engine) =>
        {
            var decision = engine.ComputeGuidance(request);
            return Results.Ok(decision);
        })
        .WithName("ComputeAerocaptureGuidance")
        .WithSummary("Executes predictor-corrector aerocapture bank angle guidance for planetary orbit insertion with zero propellant burn")
        .Produces<AerocaptureGuidanceDecision>(StatusCodes.Status200OK);

        return endpoints;
    }
}
