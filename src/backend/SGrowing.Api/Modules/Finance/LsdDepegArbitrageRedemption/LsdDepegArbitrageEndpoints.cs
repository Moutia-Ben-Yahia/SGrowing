using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.LsdDepegArbitrageRedemption;

public static class LsdDepegArbitrageEndpoints
{
    public static IServiceCollection AddLsdDepegArbitrageServices(this IServiceCollection services)
    {
        services.AddScoped<ILsdDepegArbitrageRedemptionEngine, LsdDepegArbitrageRedemptionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLsdDepegArbitrageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/lsd-depeg-arbitrage")
            .WithTags("Finance - Cross-Chain Liquid Staking Depeg Arbitrage & Redemption");

        group.MapPost("/analyze-arbitrage", (
            LsdArbitrageTelemetry telemetry,
            ILsdDepegArbitrageRedemptionEngine engine) =>
        {
            var result = engine.AnalyzeLsdDepegArbitrage(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLsdDepegArbitrage")
        .WithSummary("Analyzes liquid staking derivative (LSD) depeg discount, validator unstaking queue delay, and triangular arbitrage profitability.");

        return app;
    }
}
