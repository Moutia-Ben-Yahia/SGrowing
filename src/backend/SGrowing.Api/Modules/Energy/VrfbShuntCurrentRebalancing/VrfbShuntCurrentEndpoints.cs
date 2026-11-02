using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.VrfbShuntCurrentRebalancing;

public static class VrfbShuntCurrentEndpoints
{
    public static IServiceCollection AddVrfbShuntCurrentServices(this IServiceCollection services)
    {
        services.AddScoped<IVrfbShuntCurrentEngine, VrfbShuntCurrentEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVrfbShuntCurrentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/vrfb-shunt-current-rebalancing")
            .WithTags("Energy All-Vanadium Redox Flow Battery (VRFB) Shunt Currents & Rebalancing");

        group.MapPost("/analyze", (VrfbShuntCurrentTelemetry telemetry, IVrfbShuntCurrentEngine engine) =>
        {
            var evaluation = engine.AnalyzeStack(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeVrfbShuntCurrent")
        .WithSummary("Analyzes all-vanadium redox flow batteries (VRFB), manifold parasitic shunt current losses, and electrolyte SOC rebalancing");

        return endpoints;
    }
}
