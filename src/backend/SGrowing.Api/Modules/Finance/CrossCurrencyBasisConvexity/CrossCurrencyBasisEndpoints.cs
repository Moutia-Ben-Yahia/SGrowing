using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CrossCurrencyBasisConvexity;

public static class CrossCurrencyBasisEndpoints
{
    public static IServiceCollection AddCrossCurrencyBasisServices(this IServiceCollection services)
    {
        services.AddScoped<ICrossCurrencyBasisEngine, CrossCurrencyBasisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrossCurrencyBasisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/cross-currency-basis-convexity")
            .WithTags("Finance Cross-Currency Basis Swaps & Convexity Conundrum");

        group.MapPost("/analyze", (CrossCurrencyBasisTelemetry telemetry, ICrossCurrencyBasisEngine engine) =>
        {
            var evaluation = engine.AnalyzePortfolio(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeCrossCurrencyBasis")
        .WithSummary("Analyzes cross-currency CIP basis dislocations, negative convexity duration cascades, and CSA collateral discounting");

        return endpoints;
    }
}
