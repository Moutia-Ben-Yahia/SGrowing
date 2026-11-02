using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SolidOxideFuelCellTriplePhaseBoundary;

public static class SolidOxideFuelCellEndpoints
{
    public static IServiceCollection AddSolidOxideFuelCellServices(this IServiceCollection services)
    {
        services.AddScoped<ISolidOxideFuelCellEngine, SolidOxideFuelCellEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSolidOxideFuelCellEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/solid-oxide-fuel-cell")
            .WithTags("Energy Solid Oxide Fuel Cell Triple-Phase Boundary");

        group.MapPost("/analyze", (SolidOxideFuelCellTelemetry telemetry, ISolidOxideFuelCellEngine engine) =>
        {
            var evaluation = engine.AnalyzeFuelCell(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSolidOxideFuelCell")
        .WithSummary("Analyzes high-temperature SOFC electrochemical polarizations, triple-phase boundary kinetics, and carbon coking risks");

        return endpoints;
    }
}
