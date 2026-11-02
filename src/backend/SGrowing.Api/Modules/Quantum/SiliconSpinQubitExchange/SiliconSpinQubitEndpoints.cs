using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.SiliconSpinQubitExchange;

public static class SiliconSpinQubitEndpoints
{
    public static IServiceCollection AddSiliconSpinQubitServices(this IServiceCollection services)
    {
        services.AddScoped<ISiliconSpinQubitEngine, SiliconSpinQubitEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSiliconSpinQubitEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/silicon-spin-qubit-exchange")
            .WithTags("Quantum Silicon Spin Qubit Exchange Coupling");

        group.MapPost("/analyze", (SiliconSpinQubitTelemetry telemetry, ISiliconSpinQubitEngine engine) =>
        {
            var evaluation = engine.AnalyzeSpinQubit(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSiliconSpinQubit")
        .WithSummary("Analyzes silicon quantum dot exchange coupling J(eps), charge noise sweet spots, and two-qubit logic gate fidelities");

        return endpoints;
    }
}
