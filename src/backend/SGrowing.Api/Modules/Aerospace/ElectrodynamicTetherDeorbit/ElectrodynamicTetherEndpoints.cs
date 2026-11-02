using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.ElectrodynamicTetherDeorbit;

public static class ElectrodynamicTetherEndpoints
{
    public static IServiceCollection AddElectrodynamicTetherDeorbitServices(this IServiceCollection services)
    {
        services.AddScoped<IElectrodynamicTetherDeorbitEngine, ElectrodynamicTetherDeorbitEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapElectrodynamicTetherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/electrodynamic-tether")
            .WithTags("Aerospace - Electrodynamic Space Tether Orbital Deorbiting");

        group.MapPost("/analyze-tether", (
            ElectrodynamicTetherTelemetry telemetry,
            IElectrodynamicTetherDeorbitEngine engine) =>
        {
            var result = engine.AnalyzeElectrodynamicTether(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeElectrodynamicTetherDeorbit")
        .WithSummary("Analyzes electrodynamic bare tether deorbiting, induced motional EMF, Lorentz deceleration drag, and libration stability.");

        return app;
    }
}
