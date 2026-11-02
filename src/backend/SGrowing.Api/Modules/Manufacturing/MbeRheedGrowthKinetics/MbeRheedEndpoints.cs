using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Manufacturing.MbeRheedGrowthKinetics;

public static class MbeRheedEndpoints
{
    public static IServiceCollection AddMbeRheedGrowthServices(this IServiceCollection services)
    {
        services.AddScoped<IMbeRheedGrowthKineticsEngine, MbeRheedGrowthKineticsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMbeRheedGrowthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/manufacturing/mbe-rheed")
            .WithTags("Manufacturing - Molecular Beam Epitaxy RHEED Growth Kinetics");

        group.MapPost("/analyze-growth", (
            MbeGrowthTelemetry telemetry,
            IMbeRheedGrowthKineticsEngine engine) =>
        {
            var result = engine.AnalyzeEpitaxialGrowth(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMbeGrowth")
        .WithSummary("Analyzes UHV molecular beam epitaxy (MBE) flux ratio, RHEED intensity oscillations, and 2D/3D growth kinetics.");

        return app;
    }
}
