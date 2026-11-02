using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.NeutralAtomRydbergGates;

public static class NeutralAtomRydbergEndpoints
{
    public static IServiceCollection AddNeutralAtomRydbergServices(this IServiceCollection services)
    {
        services.AddScoped<INeutralAtomRydbergGatesEngine, NeutralAtomRydbergGatesEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNeutralAtomRydbergEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/neutral-atom-rydberg")
            .WithTags("Quantum Neutral Atom Rydberg Gates");

        group.MapPost("/analyze", (NeutralAtomRydbergTelemetry telemetry, INeutralAtomRydbergGatesEngine engine) =>
        {
            var result = engine.AnalyzeRydbergGate(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeNeutralAtomRydbergGate")
        .WithSummary("Analyzes neutral atom optical tweezer register Rydberg blockade radius, interaction energy, and two-qubit gate fidelity.");

        return endpoints;
    }
}
