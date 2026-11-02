using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.HallEffectThrusterErosion;

public static class HallEffectThrusterEndpoints
{
    public static IServiceCollection AddHallEffectThrusterErosionServices(this IServiceCollection services)
    {
        services.AddScoped<IHallEffectThrusterErosionEngine, HallEffectThrusterErosionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHallEffectThrusterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/hall-effect-thruster-erosion")
            .WithTags("Aerospace Hall-Effect Thruster Erosion");

        group.MapPost("/analyze", (HallEffectThrusterTelemetry telemetry, IHallEffectThrusterErosionEngine engine) =>
        {
            var result = engine.AnalyzeThruster(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHallEffectThruster")
        .WithSummary("Analyzes Hall-effect electric propulsion channel wall sputtering erosion, 10-30 kHz breathing mode plasma oscillations, and specific impulse efficiency.");

        return endpoints;
    }
}
