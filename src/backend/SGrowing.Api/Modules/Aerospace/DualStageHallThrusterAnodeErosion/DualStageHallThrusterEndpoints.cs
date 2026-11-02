using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.DualStageHallThrusterAnodeErosion;

public static class DualStageHallThrusterEndpoints
{
    public static IServiceCollection AddDualStageHallThrusterServices(this IServiceCollection services)
    {
        services.AddScoped<IDualStageHallThrusterAnodeErosionEngine, DualStageHallThrusterAnodeErosionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDualStageHallThrusterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/dual-stage-hall")
            .WithTags("Aerospace - Dual-Stage Hall Thruster Anode Erosion");

        group.MapPost("/analyze-thruster", (
            DualStageHallTelemetry telemetry,
            IDualStageHallThrusterAnodeErosionEngine engine) =>
        {
            var result = engine.AnalyzeThrusterErosionAndThrust(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHallThruster")
        .WithSummary("Analyzes dual-stage Hall effect thruster ionization, acceleration voltage, ceramic channel wall erosion, and gimbal vectoring.");

        return app;
    }
}
