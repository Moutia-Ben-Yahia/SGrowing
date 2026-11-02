using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SolidStateBatteryDendriteSuppression;

public static class SolidStateBatteryDendriteEndpoints
{
    public static IServiceCollection AddSolidStateBatteryDendriteServices(this IServiceCollection services)
    {
        services.AddScoped<ISolidStateBatteryDendriteEngine, SolidStateBatteryDendriteEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSolidStateBatteryDendriteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/solid-state-battery-dendrite")
            .WithTags("Energy Solid-State Battery Dendrite Suppression");

        group.MapPost("/analyze", (SolidStateBatteryDendriteTelemetry telemetry, ISolidStateBatteryDendriteEngine engine) =>
        {
            var result = engine.AnalyzeDendriteSuppression(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSolidStateBatteryDendrite")
        .WithSummary("Analyzes all-solid-state lithium metal battery (SSB) intergranular dendrite propagation, uniaxial stack pressure tuning, and critical current density (j_CCD).");

        return endpoints;
    }
}
