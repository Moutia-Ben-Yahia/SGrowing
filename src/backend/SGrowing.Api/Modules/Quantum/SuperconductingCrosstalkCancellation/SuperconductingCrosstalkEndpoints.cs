using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.SuperconductingCrosstalkCancellation;

public static class SuperconductingCrosstalkEndpoints
{
    public static IServiceCollection AddSuperconductingCrosstalkCancellationServices(this IServiceCollection services)
    {
        services.AddScoped<ISuperconductingCrosstalkEngine, SuperconductingCrosstalkEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSuperconductingCrosstalkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/superconducting-crosstalk-cancellation")
            .WithTags("Quantum Superconducting Crosstalk Cancellation");

        group.MapPost("/analyze", (SuperconductingCrosstalkTelemetry telemetry, ISuperconductingCrosstalkEngine engine) =>
        {
            var result = engine.AnalyzeCrosstalk(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSuperconductingCrosstalk")
        .WithSummary("Analyzes multi-qubit superconducting quantum processor flux mutual crosstalk matrix inversion, residual static ZZ-coupling, and two-qubit CZ gate fidelities.");

        return endpoints;
    }
}
