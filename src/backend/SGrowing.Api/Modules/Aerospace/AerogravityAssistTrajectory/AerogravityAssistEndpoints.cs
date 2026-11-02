using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.AerogravityAssistTrajectory;

public static class AerogravityAssistEndpoints
{
    public static IServiceCollection AddAerogravityAssistTrajectoryServices(this IServiceCollection services)
    {
        services.AddScoped<IAerogravityAssistTrajectoryEngine, AerogravityAssistTrajectoryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAerogravityAssistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/aerogravity-assist")
            .WithTags("Aerospace - Aerogravity-Assist (AGA) Hypersonic Trajectory");

        group.MapPost("/analyze-trajectory", (
            AerogravityAssistTelemetry telemetry,
            IAerogravityAssistTrajectoryEngine engine) =>
        {
            var result = engine.AnalyzeAerogravityAssist(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAerogravityAssistTrajectory")
        .WithSummary("Analyzes planetary aerogravity-assist (AGA) waverider trajectories, inverted downward lift, turning angle amplification, and thermal heating.");

        return app;
    }
}
