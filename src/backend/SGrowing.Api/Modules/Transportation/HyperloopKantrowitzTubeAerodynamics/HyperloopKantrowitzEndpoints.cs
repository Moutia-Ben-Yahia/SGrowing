using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Transportation.HyperloopKantrowitzTubeAerodynamics;

public static class HyperloopKantrowitzEndpoints
{
    public static IServiceCollection AddHyperloopKantrowitzAerodynamicsServices(this IServiceCollection services)
    {
        services.AddScoped<IHyperloopKantrowitzTubeAerodynamicsEngine, HyperloopKantrowitzTubeAerodynamicsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHyperloopKantrowitzEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/transportation/hyperloop-kantrowitz-aerodynamics")
            .WithTags("Transportation - Hyperloop Kantrowitz Compressible Aerodynamics & Tube Depressurization");

        group.MapPost("/analyze-aerodynamics", (
            HyperloopTubeTelemetry telemetry,
            IHyperloopKantrowitzTubeAerodynamicsEngine engine) =>
        {
            var result = engine.AnalyzeTubeAerodynamics(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHyperloopKantrowitzAerodynamics")
        .WithSummary("Analyzes low-pressure tube pod aerodynamics, Kantrowitz choking Mach boundary, nose compressor bypass, and piston drag.");

        return app;
    }
}
