using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.SuperconductingTwpaReadout;

public static class SuperconductingTwpaEndpoints
{
    public static IServiceCollection AddSuperconductingTwpaReadoutServices(this IServiceCollection services)
    {
        services.AddScoped<ISuperconductingTwpaReadoutEngine, SuperconductingTwpaReadoutEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSuperconductingTwpaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/superconducting-twpa-readout")
            .WithTags("Quantum Superconducting TWPA Readout");

        group.MapPost("/analyze", (SuperconductingTwpaTelemetry telemetry, ISuperconductingTwpaReadoutEngine engine) =>
        {
            var result = engine.AnalyzeTwpaPerformance(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSuperconductingTwpa")
        .WithSummary("Analyzes superconducting traveling wave parametric amplifier (TWPA) broadband gain, quantum-limited noise floor, and multi-qubit multiplexed readout capacity.");

        return endpoints;
    }
}
