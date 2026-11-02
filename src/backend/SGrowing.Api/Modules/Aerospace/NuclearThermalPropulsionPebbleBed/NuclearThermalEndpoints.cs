using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.NuclearThermalPropulsionPebbleBed;

public static class NuclearThermalEndpoints
{
    public static IServiceCollection AddNuclearThermalPropulsionPebbleBedServices(this IServiceCollection services)
    {
        services.AddScoped<INuclearThermalPropulsionPebbleBedEngine, NuclearThermalPropulsionPebbleBedEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNuclearThermalPropulsionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/nuclear-thermal-pebble-bed")
            .WithTags("Aerospace Nuclear Thermal Propulsion Pebble-Bed");

        group.MapPost("/analyze", (NuclearThermalTelemetry telemetry, INuclearThermalPropulsionPebbleBedEngine engine) =>
        {
            var result = engine.AnalyzeReactorPropulsion(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeNuclearThermalPropulsion")
        .WithSummary("Analyzes high-temperature pebble-bed nuclear thermal propulsion, hydrogen thermal dissociation, specific impulse (> 950 s), and fuel safety margins.");

        return endpoints;
    }
}
