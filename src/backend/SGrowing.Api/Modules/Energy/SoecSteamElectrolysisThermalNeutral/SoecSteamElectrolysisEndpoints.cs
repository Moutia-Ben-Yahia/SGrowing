using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SoecSteamElectrolysisThermalNeutral;

public static class SoecSteamElectrolysisEndpoints
{
    public static IServiceCollection AddSoecSteamElectrolysisServices(this IServiceCollection services)
    {
        services.AddScoped<ISoecSteamElectrolysisEngine, SoecSteamElectrolysisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSoecSteamElectrolysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/soec-steam-electrolysis-thermal-neutral")
            .WithTags("Energy Solid Oxide Electrolyzer (SOEC) High-Temperature Steam Electrolysis");

        group.MapPost("/analyze", (SoecSteamElectrolysisTelemetry telemetry, ISoecSteamElectrolysisEngine engine) =>
        {
            var evaluation = engine.AnalyzeStack(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSoecSteamElectrolysis")
        .WithSummary("Analyzes high-temperature SOEC steam electrolysis, thermal-neutral cell operating potential, area-specific resistance, and electrode delamination");

        return endpoints;
    }
}
