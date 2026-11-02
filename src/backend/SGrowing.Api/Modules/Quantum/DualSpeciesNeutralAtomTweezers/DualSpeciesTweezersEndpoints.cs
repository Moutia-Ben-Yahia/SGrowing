using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.DualSpeciesNeutralAtomTweezers;

public static class DualSpeciesTweezersEndpoints
{
    public static IServiceCollection AddDualSpeciesTweezersServices(this IServiceCollection services)
    {
        services.AddScoped<IDualSpeciesTweezersEngine, DualSpeciesTweezersEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDualSpeciesTweezersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/dual-species-neutral-atom-tweezers")
            .WithTags("Quantum Dual-Species Optical Tweezers & Inter-Species Rydberg Gates");

        group.MapPost("/analyze", (DualSpeciesTweezersTelemetry telemetry, IDualSpeciesTweezersEngine engine) =>
        {
            var evaluation = engine.AnalyzeTweezers(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeDualSpeciesTweezers")
        .WithSummary("Analyzes dual-species neutral atom tweezer arrays, inter-species Rydberg blockade, shuffling survival, and mid-circuit imaging crosstalk");

        return endpoints;
    }
}
