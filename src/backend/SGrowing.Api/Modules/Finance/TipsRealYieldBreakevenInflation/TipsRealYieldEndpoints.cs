using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.TipsRealYieldBreakevenInflation;

public static class TipsRealYieldEndpoints
{
    public static IServiceCollection AddTipsRealYieldServices(this IServiceCollection services)
    {
        services.AddScoped<ITipsRealYieldEngine, TipsRealYieldEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTipsRealYieldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/tips-real-yield-breakeven")
            .WithTags("Finance Inflation-Linked Bonds (TIPS) Real Yield & Breakeven Inflation");

        group.MapPost("/analyze", (TipsRealYieldTelemetry telemetry, ITipsRealYieldEngine engine) =>
        {
            var evaluation = engine.AnalyzeRealYield(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeTipsRealYield")
        .WithSummary("Analyzes TIPS real yields, breakeven inflation curve term structure, risk premia, and deflation floor protection");

        return endpoints;
    }
}
