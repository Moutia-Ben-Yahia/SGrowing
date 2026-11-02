using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.FxHedging;

public static class FxHedgingEndpoints
{
    public static IServiceCollection AddFxHedgingServices(this IServiceCollection services)
    {
        services.AddSingleton<ValueAtRiskExposureEngine>();
        services.AddSingleton<TriangularArbitrageDetectorService>();
        return services;
    }

    public static IEndpointRouteBuilder MapFxHedgingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/fx-hedging")
            .WithTags("Real-Time Algorithmic Cross-Border FX Hedging & Currency Exposure Radar");

        group.MapPost("/var/evaluate", (
            EvaluateFxRiskRequest request,
            ValueAtRiskExposureEngine engine) =>
        {
            var report = engine.EvaluatePortfolioRisk(request);
            return Results.Ok(report);
        });

        group.MapPost("/arbitrage/scan", (
            ScanArbitrageApiRequest request,
            TriangularArbitrageDetectorService scanner) =>
        {
            var opp = scanner.DetectTriangularMisalignment(
                request.Base,
                request.Intermediate,
                request.Quote,
                request.RateBaseToIntermediate,
                request.RateIntermediateToQuote,
                request.MarketRateBaseToQuote);

            return Results.Ok(opp);
        });

        return app;
    }
}

public sealed record ScanArbitrageApiRequest(
    CurrencyCode Base,
    CurrencyCode Intermediate,
    CurrencyCode Quote,
    decimal RateBaseToIntermediate,
    decimal RateIntermediateToQuote,
    decimal MarketRateBaseToQuote
);
