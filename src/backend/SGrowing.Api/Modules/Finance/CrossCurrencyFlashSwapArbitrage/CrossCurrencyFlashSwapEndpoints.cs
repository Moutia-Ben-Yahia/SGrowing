using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CrossCurrencyFlashSwapArbitrage;

public static class CrossCurrencyFlashSwapEndpoints
{
    public static IServiceCollection AddCrossCurrencyFlashSwapServices(this IServiceCollection services)
    {
        services.AddScoped<ICrossCurrencyFlashSwapArbitrageEngine, CrossCurrencyFlashSwapArbitrageEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrossCurrencyFlashSwapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/cross-currency-flash-swap")
            .WithTags("Finance Cross-Currency Flash Swap Triangular Arbitrage");

        group.MapPost("/analyze", (CrossCurrencyFlashSwapTelemetry telemetry, ICrossCurrencyFlashSwapArbitrageEngine engine) =>
        {
            var result = engine.AnalyzeTriangularArbitrage(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCrossCurrencyFlashSwapArbitrage")
        .WithSummary("Analyzes cyclic triangular AMM arbitrage paths, fee tier retention, optimal flash borrow sizing, and private MEV builder bundles.");

        return endpoints;
    }
}
