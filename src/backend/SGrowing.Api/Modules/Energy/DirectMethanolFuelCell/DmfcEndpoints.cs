using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.DirectMethanolFuelCell;

public static class DmfcEndpoints
{
    public static IServiceCollection AddDirectMethanolFuelCellServices(this IServiceCollection services)
    {
        services.AddScoped<IDirectMethanolFuelCellEngine, DirectMethanolFuelCellEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDmfcEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/direct-methanol-fuel-cell")
            .WithTags("Energy Direct Methanol Fuel Cell");

        group.MapPost("/analyze", (DmfcTelemetry telemetry, IDirectMethanolFuelCellEngine engine) =>
        {
            var result = engine.AnalyzeFuelCell(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDirectMethanolFuelCell")
        .WithSummary("Analyzes DMFC anode methanol oxidation, Nafion crossover diffusion, cathode mixed potential, and power density.");

        return endpoints;
    }
}
