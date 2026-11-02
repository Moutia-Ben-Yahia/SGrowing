using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.RotatingDetonationRocketCombustor;

public static class RotatingDetonationEndpoints
{
    public static IServiceCollection AddRotatingDetonationRocketCombustorServices(this IServiceCollection services)
    {
        services.AddScoped<IRotatingDetonationRocketCombustorEngine, RotatingDetonationRocketCombustorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRotatingDetonationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/rotating-detonation-combustor")
            .WithTags("Aerospace Rotating Detonation Rocket Combustor");

        group.MapPost("/analyze", (RotatingDetonationTelemetry telemetry, IRotatingDetonationRocketCombustorEngine engine) =>
        {
            var result = engine.AnalyzeDetonationCombustor(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeRotatingDetonationCombustor")
        .WithSummary("Analyzes rotating detonation rocket engine (RDRE) continuous supersonic wave kinematics, CJ velocity deficit, and pressure-gain combustion stability.");

        return endpoints;
    }
}
