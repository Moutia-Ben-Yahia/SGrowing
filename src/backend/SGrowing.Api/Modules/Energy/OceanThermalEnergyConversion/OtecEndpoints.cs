using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.OceanThermalEnergyConversion;

public static class OtecEndpoints
{
    public static IServiceCollection AddOceanThermalEnergyConversionServices(this IServiceCollection services)
    {
        services.AddScoped<IOceanThermalEnergyConversionEngine, OceanThermalEnergyConversionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOtecEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/ocean-thermal-energy-conversion")
            .WithTags("Energy Ocean Thermal Energy Conversion (OTEC)");

        group.MapPost("/analyze", (OtecTelemetry telemetry, IOceanThermalEnergyConversionEngine engine) =>
        {
            var result = engine.AnalyzeOtecPlant(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeOceanThermalEnergyConversion")
        .WithSummary("Analyzes closed-cycle ammonia OTEC plant, ocean delta-T, deep seawater upwelling pump head, and net export electric power.");

        return endpoints;
    }
}
