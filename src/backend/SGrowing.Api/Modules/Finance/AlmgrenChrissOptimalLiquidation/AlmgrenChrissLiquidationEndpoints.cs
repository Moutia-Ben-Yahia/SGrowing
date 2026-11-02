using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.AlmgrenChrissOptimalLiquidation;

public static class AlmgrenChrissLiquidationEndpoints
{
    public static IServiceCollection AddAlmgrenChrissLiquidationServices(this IServiceCollection services)
    {
        services.AddScoped<IAlmgrenChrissLiquidationEngine, AlmgrenChrissLiquidationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAlmgrenChrissLiquidationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/almgren-chriss-liquidation")
            .WithTags("Finance Almgren-Chriss Optimal Liquidation & VWAP Slippage");

        group.MapPost("/analyze", (AlmgrenChrissLiquidationTelemetry telemetry, IAlmgrenChrissLiquidationEngine engine) =>
        {
            var evaluation = engine.AnalyzeLiquidation(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeAlmgrenChrissLiquidation")
        .WithSummary("Analyzes institutional parent order execution trajectories, market impact, and Almgren-Chriss implementation shortfall");

        return endpoints;
    }
}
