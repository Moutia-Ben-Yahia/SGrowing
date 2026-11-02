using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.StatisticalArbitrageOrnsteinUhlenbeck;

public static class StatisticalArbitrageEndpoints
{
    public static IServiceCollection AddStatisticalArbitrageOrnsteinUhlenbeckServices(this IServiceCollection services)
    {
        services.AddScoped<IStatisticalArbitrageOrnsteinUhlenbeckEngine, StatisticalArbitrageOrnsteinUhlenbeckEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapStatisticalArbitrageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/statistical-arbitrage-ornstein-uhlenbeck")
            .WithTags("Finance Statistical Arbitrage Ornstein-Uhlenbeck");

        group.MapPost("/analyze", (StatisticalArbitrageTelemetry telemetry, IStatisticalArbitrageOrnsteinUhlenbeckEngine engine) =>
        {
            var result = engine.AnalyzePairMeanReversion(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzePairMeanReversion")
        .WithSummary("Analyzes cointegrated asset pair residual mean-reversion, Ornstein-Uhlenbeck process half-life, and statistical arbitrage Z-score bounds.");

        return endpoints;
    }
}
