using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.GravitationalInterferometerMesh;

public static class GravitationalInterferometerEndpoints
{
    public static IServiceCollection AddGravitationalInterferometerServices(this IServiceCollection services)
    {
        services.AddSingleton<IGravitationalInterferometerMeshEngine, GravitationalInterferometerMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGravitationalInterferometerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/gravitational-interferometer")
            .WithTags("Aerospace - Deep-Space Gravitational Wave Interferometer");

        group.MapPost("/synthesize-tdi", (
            TdiSynthesisRequest request,
            IGravitationalInterferometerMeshEngine engine) =>
        {
            var result = engine.SynthesizeTdiAndDetectStrain(request);
            return Results.Ok(result);
        })
        .WithName("SynthesizeTdiGravitationalStrain")
        .WithSummary("Synthesizes Time-Delay Interferometry (TDI) equal-arm combinations and detects relativistic gravitational wave strain")
        .Produces<GravitationalStrainDetectionResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
