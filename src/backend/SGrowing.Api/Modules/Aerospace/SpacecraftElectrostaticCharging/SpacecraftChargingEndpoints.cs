using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.SpacecraftElectrostaticCharging;

public static class SpacecraftChargingEndpoints
{
    public static IServiceCollection AddSpacecraftChargingServices(this IServiceCollection services)
    {
        services.AddScoped<ISpacecraftChargingEngine, SpacecraftChargingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpacecraftChargingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/spacecraft-electrostatic-charging")
            .WithTags("Aerospace Spacecraft Electrostatic Charging & Magnetospheric Arc Mitigation");

        group.MapPost("/analyze", (SpacecraftChargingTelemetry telemetry, ISpacecraftChargingEngine engine) =>
        {
            var evaluation = engine.AnalyzeCharging(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSpacecraftCharging")
        .WithSummary("Analyzes spacecraft differential dielectric charging, substorm frame potentials, and plasma contactor emission margins");

        return endpoints;
    }
}
