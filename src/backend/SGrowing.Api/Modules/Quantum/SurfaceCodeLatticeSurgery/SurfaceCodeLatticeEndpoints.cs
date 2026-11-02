using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.SurfaceCodeLatticeSurgery;

public static class SurfaceCodeLatticeEndpoints
{
    public static IServiceCollection AddSurfaceCodeLatticeServices(this IServiceCollection services)
    {
        services.AddScoped<ISurfaceCodeLatticeEngine, SurfaceCodeLatticeEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSurfaceCodeLatticeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/surface-code-lattice-surgery")
            .WithTags("Quantum Rotated Surface Code & Fault-Tolerant Lattice Surgery");

        group.MapPost("/analyze", (SurfaceCodeLatticeTelemetry telemetry, ISurfaceCodeLatticeEngine engine) =>
        {
            var evaluation = engine.AnalyzeLattice(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSurfaceCodeLattice")
        .WithSummary("Analyzes rotated surface code patches, syndrome decoding latency, defect chain leakage, and transversal lattice surgery fault tolerance");

        return endpoints;
    }
}
