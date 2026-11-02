using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.AerogelTpsAblationRecession;

public static class AerogelTpsEndpoints
{
    public static IServiceCollection AddAerogelTpsAblationRecessionServices(this IServiceCollection services)
    {
        services.AddScoped<IAerogelTpsAblationRecessionEngine, AerogelTpsAblationRecessionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAerogelTpsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/aerogel-tps-ablation")
            .WithTags("Aerospace - Hypersonic Aerogel TPS Thermal Shock & Ablation Recession");

        group.MapPost("/analyze-tps", (
            AerogelTpsTelemetry telemetry,
            IAerogelTpsAblationRecessionEngine engine) =>
        {
            var result = engine.AnalyzeTpsAblationRecession(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAerogelTpsAblation")
        .WithSummary("Analyzes hypersonic vehicle aerogel thermal protection system (TPS) surface ablation recession, bondline heat soak, and thermal shock stress.");

        return app;
    }
}
