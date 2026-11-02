using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.NeutralAtomTweezerRearrangement;

public static class NeutralAtomTweezerEndpoints
{
    public static IServiceCollection AddNeutralAtomTweezerRearrangementServices(this IServiceCollection services)
    {
        services.AddScoped<INeutralAtomTweezerRearrangementEngine, NeutralAtomTweezerRearrangementEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNeutralAtomTweezerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/neutral-atom-tweezer-rearrangement")
            .WithTags("Quantum Neutral Atom Tweezer Rearrangement");

        group.MapPost("/analyze", (NeutralAtomTweezerTelemetry telemetry, INeutralAtomTweezerRearrangementEngine engine) =>
        {
            var result = engine.AnalyzeRearrangement(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeNeutralAtomTweezerRearrangement")
        .WithSummary("Analyzes neutral atom optical tweezer spatial shuttling, adiabatic transport limits, collisionless clearance separation, and defect-free register initialization.");

        return endpoints;
    }
}
