using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.MiniMagnetosphericPlasmaPropulsion;

public static class M2p2Endpoints
{
    public static IServiceCollection AddM2p2Services(this IServiceCollection services)
    {
        services.AddScoped<IMiniMagnetosphericPlasmaPropulsionEngine, MiniMagnetosphericPlasmaPropulsionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapM2p2Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/m2p2-propulsion")
            .WithTags("Aerospace - Mini-Magnetospheric Plasma Propulsion & Radiation Shielding");

        group.MapPost("/analyze-magnetosphere", (
            M2p2Telemetry telemetry,
            IMiniMagnetosphericPlasmaPropulsionEngine engine) =>
        {
            var result = engine.AnalyzePlasmaMagnetosphere(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeM2p2PlasmaMagnetosphere")
        .WithSummary("Analyzes mini-magnetospheric plasma propulsion (M2P2), solar wind dynamic pressure momentum transfer, and deep-space radiation shielding.");

        return app;
    }
}
