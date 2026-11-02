using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.ConcentratedLiquidity;

public static class ConcentratedLiquidityEndpoints
{
    public static IServiceCollection AddConcentratedLiquidityServices(this IServiceCollection services)
    {
        services.AddSingleton<ConcentratedLiquidityAmmEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapConcentratedLiquidityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/concentrated-liquidity")
            .WithTags("Autonomous High-Frequency Automated Market Maker (AMM) & Concentrated Liquidity Engine");

        group.MapPost("/efficiency", (
            CalculateEfficiencyApiRequest request,
            ConcentratedLiquidityAmmEngine engine) =>
        {
            var multiplier = engine.CalculateCapitalEfficiencyMultiplier(request.LowerPrice, request.UpperPrice);
            return Results.Ok(new { Multiplier = multiplier });
        });

        return app;
    }
}

public sealed record CalculateEfficiencyApiRequest(double LowerPrice, double UpperPrice);
