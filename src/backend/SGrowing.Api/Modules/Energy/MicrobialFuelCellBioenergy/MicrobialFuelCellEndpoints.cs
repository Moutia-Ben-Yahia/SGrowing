using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.MicrobialFuelCellBioenergy;

public static class MicrobialFuelCellEndpoints
{
    public static IServiceCollection AddMicrobialFuelCellBioenergyServices(this IServiceCollection services)
    {
        services.AddScoped<IMicrobialFuelCellBioenergyEngine, MicrobialFuelCellBioenergyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicrobialFuelCellEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/microbial-fuel-cell")
            .WithTags("Energy Microbial Fuel Cell Bioenergy");

        group.MapPost("/analyze", (MicrobialFuelCellTelemetry telemetry, IMicrobialFuelCellBioenergyEngine engine) =>
        {
            var result = engine.AnalyzeBioenergyProcess(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMicrobialFuelCellBioenergy")
        .WithSummary("Analyzes wastewater COD removal, Coulombic efficiency, internal resistance, and bioelectricity generation power density.");

        return endpoints;
    }
}
