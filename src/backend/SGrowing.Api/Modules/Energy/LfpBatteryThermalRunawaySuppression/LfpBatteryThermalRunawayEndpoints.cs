using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.LfpBatteryThermalRunawaySuppression;

public static class LfpBatteryThermalRunawayEndpoints
{
    public static IServiceCollection AddLfpBatteryThermalRunawayServices(this IServiceCollection services)
    {
        services.AddScoped<ILfpBatteryThermalRunawaySuppressionEngine, LfpBatteryThermalRunawaySuppressionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLfpBatteryThermalRunawayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/lfp-battery-thermal-runaway")
            .WithTags("Energy LFP Battery Thermal Runaway & Fire Suppression");

        group.MapPost("/analyze", (LfpBatteryThermalRunawayTelemetry telemetry, ILfpBatteryThermalRunawaySuppressionEngine engine) =>
        {
            var result = engine.AnalyzeBatterySafety(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLfpBatteryThermalSafety")
        .WithSummary("Analyzes grid-scale lithium-iron-phosphate (LFP) BESS container off-gas venting (CO, H2), thermal runaway rate-of-rise, and emergency deluge suppression.");

        return endpoints;
    }
}
