using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.AmmDynamicImpermanentLossHedge;

public static class AmmDynamicHedgeEndpoints
{
    public static IServiceCollection AddAmmDynamicImpermanentLossHedgeServices(this IServiceCollection services)
    {
        services.AddScoped<IAmmDynamicImpermanentLossHedgeEngine, AmmDynamicImpermanentLossHedgeEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAmmDynamicHedgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/amm-impermanent-loss-hedge")
            .WithTags("Finance AMM Dynamic Impermanent Loss Hedge");

        group.MapPost("/analyze", (AmmDynamicHedgeTelemetry telemetry, IAmmDynamicImpermanentLossHedgeEngine engine) =>
        {
            var result = engine.AnalyzeHedge(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAmmDynamicHedge")
        .WithSummary("Analyzes AMM constant product liquidity pool impermanent loss, quadratic option replication, and dynamic perpetual delta hedge.");

        return endpoints;
    }
}
