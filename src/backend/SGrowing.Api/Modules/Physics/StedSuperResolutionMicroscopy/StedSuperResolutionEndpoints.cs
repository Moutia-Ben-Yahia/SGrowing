using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Physics.StedSuperResolutionMicroscopy;

public static class StedSuperResolutionEndpoints
{
    public static IServiceCollection AddStedSuperResolutionServices(this IServiceCollection services)
    {
        services.AddScoped<IStedSuperResolutionMicroscopyEngine, StedSuperResolutionMicroscopyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapStedSuperResolutionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/physics/sted-super-resolution")
            .WithTags("Physics - STED Super-Resolution Fluorescence Microscopy");

        group.MapPost("/analyze-imaging", (
            StedMicroscopyTelemetry telemetry,
            IStedSuperResolutionMicroscopyEngine engine) =>
        {
            var result = engine.AnalyzeStedMicroscopy(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeStedSuperResolutionImaging")
        .WithSummary("Analyzes STED super-resolution sub-diffraction fluorescence imaging, doughnut beam depletion, photobleaching kinetics, and adaptive optics correction.");

        return app;
    }
}
