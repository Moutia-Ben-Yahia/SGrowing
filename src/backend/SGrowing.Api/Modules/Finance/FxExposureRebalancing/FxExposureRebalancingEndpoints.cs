using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.FxExposureRebalancing;

public static class FxExposureRebalancingEndpoints
{
    public static IServiceCollection AddFxExposureRebalancingServices(this IServiceCollection services)
    {
        services.AddSingleton<IFxExposureRebalancingEngine, FxExposureRebalancingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFxExposureRebalancingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/fx-exposure-rebalancing")
            .WithTags("Finance - Dynamic Multi-Currency FX Hedging");

        group.MapGet("/overview", (IFxExposureRebalancingEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetFxExposureRebalancingOverview")
        .WithSummary("Lists currency portfolio VaR, unhedged drift percentages, and active forward hedge contracts");

        group.MapPost("/evaluate", (EvaluateExposureCommand command, IFxExposureRebalancingEngine engine) =>
        {
            var result = engine.EvaluateExposure(command);
            return Results.Ok(result);
        })
        .WithName("EvaluateFxExposure")
        .WithSummary("Calculates net foreign currency exposure and portfolio VaR against base operating currency");

        group.MapPost("/execute-hedge", (ExecuteForwardHedgeCommand command, IFxExposureRebalancingEngine engine) =>
        {
            var result = engine.ExecuteForwardHedge(command);
            return Results.Ok(result);
        })
        .WithName("ExecuteForwardHedge")
        .WithSummary("Issues algorithmic forward hedge contract to lock currency exchange rate and immunize margin");

        return app;
    }
}
