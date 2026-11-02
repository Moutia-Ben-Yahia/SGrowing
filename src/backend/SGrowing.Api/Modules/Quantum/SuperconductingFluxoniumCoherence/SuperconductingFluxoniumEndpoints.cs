using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.SuperconductingFluxoniumCoherence;

public static class SuperconductingFluxoniumEndpoints
{
    public static IServiceCollection AddSuperconductingFluxoniumCoherenceServices(this IServiceCollection services)
    {
        services.AddScoped<ISuperconductingFluxoniumCoherenceEngine, SuperconductingFluxoniumCoherenceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSuperconductingFluxoniumEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/fluxonium-qubit-coherence")
            .WithTags("Quantum Superconducting Fluxonium Coherence");

        group.MapPost("/analyze", (SuperconductingFluxoniumTelemetry telemetry, ISuperconductingFluxoniumCoherenceEngine engine) =>
        {
            var result = engine.AnalyzeFluxoniumCoherence(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeFluxoniumCoherence")
        .WithSummary("Analyzes superconducting fluxonium qubit capacitively shunted superinductance loop coherence, sweet spot flux noise protection, and dispersive readout shifts.");

        return endpoints;
    }
}
