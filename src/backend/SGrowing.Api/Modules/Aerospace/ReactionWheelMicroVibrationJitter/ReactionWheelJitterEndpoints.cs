using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.ReactionWheelMicroVibrationJitter;

public static class ReactionWheelJitterEndpoints
{
    public static IServiceCollection AddReactionWheelJitterServices(this IServiceCollection services)
    {
        services.AddScoped<IReactionWheelJitterEngine, ReactionWheelJitterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapReactionWheelJitterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/reaction-wheel-micro-vibration-jitter")
            .WithTags("Aerospace Reaction Wheel Micro-Vibration Jitter");

        group.MapPost("/analyze", (ReactionWheelJitterTelemetry telemetry, IReactionWheelJitterEngine engine) =>
        {
            var result = engine.AnalyzeWheelJitter(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeReactionWheelJitter")
        .WithSummary("Analyzes spacecraft reaction wheel micro-vibrations, static/dynamic mass imbalance, structural resonance proximity, and optical payload Line-of-Sight (LOS) jitter.");

        return endpoints;
    }
}
