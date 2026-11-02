using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CrossCurrencyFxBasisSwapCip;

public static class CrossCurrencyFxBasisEndpoints
{
    public static IServiceCollection AddCrossCurrencyFxBasisSwapCipServices(this IServiceCollection services)
    {
        services.AddScoped<ICrossCurrencyFxBasisSwapCipEngine, CrossCurrencyFxBasisSwapCipEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrossCurrencyFxBasisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/cross-currency-fx-basis-cip")
            .WithTags("Finance Cross-Currency FX Basis Covered Interest Parity");

        group.MapPost("/analyze", (CrossCurrencyFxBasisTelemetry telemetry, ICrossCurrencyFxBasisSwapCipEngine engine) =>
        {
            var result = engine.AnalyzeCipDeviation(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCrossCurrencyBasisCip")
        .WithSummary("Analyzes cross-currency FX basis swap spreads, Covered Interest Parity (CIP) deviations, bank regulatory capital costs (SLR), and arbitrage net profitability.");

        return endpoints;
    }
}
