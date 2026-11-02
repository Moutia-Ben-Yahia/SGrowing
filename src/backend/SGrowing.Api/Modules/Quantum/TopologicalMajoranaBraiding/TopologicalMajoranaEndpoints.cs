using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.TopologicalMajoranaBraiding;

public static class TopologicalMajoranaEndpoints
{
    public static IServiceCollection AddTopologicalMajoranaBraidingServices(this IServiceCollection services)
    {
        services.AddScoped<ITopologicalMajoranaBraidingEngine, TopologicalMajoranaBraidingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTopologicalMajoranaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/topological-majorana-braiding")
            .WithTags("Quantum Topological Majorana Zero Modes Braiding");

        group.MapPost("/analyze", (TopologicalMajoranaTelemetry telemetry, ITopologicalMajoranaBraidingEngine engine) =>
        {
            var result = engine.AnalyzeMajoranaBraiding(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeTopologicalMajoranaBraiding")
        .WithSummary("Analyzes semiconductor-superconductor nanowire non-Abelian Majorana Zero Modes (MZM) braiding adiabaticity and topological fidelity.");

        return endpoints;
    }
}
