using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Marine.MhdMarineSilentPropulsion;

public static class MhdMarineEndpoints
{
    public static IServiceCollection AddMhdMarinePropulsionServices(this IServiceCollection services)
    {
        services.AddScoped<IMhdMarineSilentPropulsionEngine, MhdMarineSilentPropulsionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMhdMarinePropulsionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/marine/mhd-propulsion")
            .WithTags("Marine - Superconducting MHD Silent Propulsion");

        group.MapPost("/analyze-thruster", (
            MhdMarineTelemetry telemetry,
            IMhdMarineSilentPropulsionEngine engine) =>
        {
            var result = engine.AnalyzeMhdThruster(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMhdThruster")
        .WithSummary("Analyzes marine magnetohydrodynamic (MHD) Lorentz thrust, duct efficiency, and hydroacoustic noise suppression.");

        return app;
    }
}
