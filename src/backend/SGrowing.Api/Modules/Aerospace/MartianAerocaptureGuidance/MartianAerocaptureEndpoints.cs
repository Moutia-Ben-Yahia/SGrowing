using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.MartianAerocaptureGuidance;

public static class MartianAerocaptureEndpoints
{
    public static IServiceCollection AddMartianAerocaptureServices(this IServiceCollection services)
    {
        services.AddScoped<IMartianAerocaptureGuidanceEngine, MartianAerocaptureGuidanceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMartianAerocaptureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/martian-aerocapture")
            .WithTags("Aerospace - Martian Aerocapture Guidance & Drag Corridor");

        group.MapPost("/analyze", (
            MartianAerocaptureTelemetry telemetry,
            IMartianAerocaptureGuidanceEngine engine) =>
        {
            var result = engine.AnalyzeAerocaptureTrajectory(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMartianAerocapture")
        .WithSummary("Analyzes Mars aerocapture entry corridor, bank angle modulation, and projected post-pass apoapsis.");

        return app;
    }
}
