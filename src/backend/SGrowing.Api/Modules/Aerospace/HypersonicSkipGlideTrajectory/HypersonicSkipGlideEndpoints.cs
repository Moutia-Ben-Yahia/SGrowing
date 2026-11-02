using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.HypersonicSkipGlideTrajectory;

public static class HypersonicSkipGlideEndpoints
{
    public static IServiceCollection AddHypersonicSkipGlideServices(this IServiceCollection services)
    {
        services.AddScoped<IHypersonicSkipGlideTrajectoryEngine, HypersonicSkipGlideTrajectoryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHypersonicSkipGlideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/hypersonic-skip-glide")
            .WithTags("Aerospace - Hypersonic Glide Vehicle Skip-Glide Trajectory");

        group.MapPost("/analyze", (
            HypersonicSkipGlideTelemetry telemetry,
            IHypersonicSkipGlideTrajectoryEngine engine) =>
        {
            var result = engine.AnalyzeTrajectoryEnvelope(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHypersonicSkipGlide")
        .WithSummary("Analyzes Mach 15-20 HGV skip-glide atmospheric pull-up g-loads, convective heat flux, and TPS soak.");

        return app;
    }
}
