using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.SuperconductingQuantumAcoustics;

public static class QuantumAcousticsEndpoints
{
    public static IServiceCollection AddQuantumAcousticsServices(this IServiceCollection services)
    {
        services.AddScoped<IQuantumAcousticsEngine, QuantumAcousticsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapQuantumAcousticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/superconducting-quantum-acoustics")
            .WithTags("Quantum Superconducting Quantum Acoustic Resonators (cQAD)");

        group.MapPost("/analyze", (QuantumAcousticsTelemetry telemetry, IQuantumAcousticsEngine engine) =>
        {
            var evaluation = engine.AnalyzeAcoustics(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeQuantumAcoustics")
        .WithSummary("Analyzes superconducting quantum acoustic resonators, phononic Fock state synthesis, transmon cooperativity, and Wigner negativity");

        return endpoints;
    }
}
