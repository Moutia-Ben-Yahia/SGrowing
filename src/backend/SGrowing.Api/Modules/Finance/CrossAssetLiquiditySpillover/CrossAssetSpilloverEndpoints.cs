using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CrossAssetLiquiditySpillover;

public static class CrossAssetSpilloverEndpoints
{
    public static IServiceCollection AddCrossAssetLiquiditySpilloverServices(this IServiceCollection services)
    {
        services.AddScoped<ICrossAssetLiquiditySpilloverEngine, CrossAssetLiquiditySpilloverEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrossAssetSpilloverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/cross-asset-spillover")
            .WithTags("Finance - Cross-Asset Market Impact & High-Frequency Liquidity Spillover");

        group.MapPost("/analyze-spillover", (
            CrossAssetSpilloverTelemetry telemetry,
            ICrossAssetLiquiditySpilloverEngine engine) =>
        {
            var result = engine.AnalyzeCrossAssetSpillover(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCrossAssetSpillover")
        .WithSummary("Analyzes cross-asset market impact tensor, high-frequency liquidity spillover ratio, systemic contagion cascades, and portfolio execution risk.");

        return app;
    }
}
