using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.AsteroidKineticImpactorDeflection;

public static class AsteroidKineticImpactorEndpoints
{
    public static IServiceCollection AddAsteroidKineticImpactorServices(this IServiceCollection services)
    {
        services.AddScoped<IAsteroidKineticImpactorDeflectionEngine, AsteroidKineticImpactorDeflectionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAsteroidKineticImpactorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/asteroid-kinetic-impactor-deflection")
            .WithTags("Aerospace Asteroid Kinetic Impactor Planetary Defense");

        group.MapPost("/analyze", (AsteroidKineticImpactorTelemetry telemetry, IAsteroidKineticImpactorDeflectionEngine engine) =>
        {
            var result = engine.AnalyzeKineticImpactor(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAsteroidKineticImpactor")
        .WithSummary("Analyzes planetary defense kinetic impactor momentum transfer (beta enhancement), target asteroid orbital delta-V, and Earth B-plane miss distance.");

        return endpoints;
    }
}
