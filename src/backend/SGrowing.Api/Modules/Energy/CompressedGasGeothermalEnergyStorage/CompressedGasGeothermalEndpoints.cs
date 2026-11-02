using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.CompressedGasGeothermalEnergyStorage;

public static class CompressedGasGeothermalEndpoints
{
    public static IServiceCollection AddCompressedGasGeothermalServices(this IServiceCollection services)
    {
        services.AddScoped<ICompressedGasGeothermalEnergyStorageEngine, CompressedGasGeothermalEnergyStorageEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCompressedGasGeothermalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/compressed-gas-geothermal-energy-storage")
            .WithTags("Energy Compressed Gas Geothermal Energy Storage");

        group.MapPost("/analyze", (CompressedGasGeothermalTelemetry telemetry, ICompressedGasGeothermalEnergyStorageEngine engine) =>
        {
            var result = engine.AnalyzeGeothermalStorage(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCompressedGasGeothermalStorage")
        .WithSummary("Analyzes compressed gas geothermal energy storage (CGES) deep saline aquifer round-trip exergy efficiency surplus, geothermal heat extraction, and caprock hydrofracture safety.");

        return endpoints;
    }
}
