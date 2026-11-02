using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.VanadiumRedoxFlowBatteryBalancing;

public static class VanadiumFlowBatteryEndpoints
{
    public static IServiceCollection AddVanadiumRedoxFlowBatteryBalancingServices(this IServiceCollection services)
    {
        services.AddScoped<IVanadiumRedoxFlowBatteryBalancingEngine, VanadiumRedoxFlowBatteryBalancingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVanadiumFlowBatteryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/vanadium-redox-flow-battery-balancing")
            .WithTags("Energy Vanadium Redox Flow Battery Balancing");

        group.MapPost("/analyze", (VanadiumFlowBatteryTelemetry telemetry, IVanadiumRedoxFlowBatteryBalancingEngine engine) =>
        {
            var result = engine.AnalyzeElectrolyteBalance(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeVanadiumFlowBatteryBalancing")
        .WithSummary("Analyzes all-vanadium redox flow battery (VRFB) real-time in-line UV-Vis spectrophotometric state-of-charge, ion crossover imbalance, and thermal V2O5 precipitation risks.");

        return endpoints;
    }
}
