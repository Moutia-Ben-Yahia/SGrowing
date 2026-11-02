using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.FxLocalStochasticVolatility;

public static class FxLsvBarrierEndpoints
{
    public static IServiceCollection AddFxLsvBarrierServices(this IServiceCollection services)
    {
        services.AddScoped<IFxLsvBarrierEngine, FxLsvBarrierEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFxLsvBarrierEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/fx-lsv-barrier-options")
            .WithTags("Finance FX Local Stochastic Volatility (LSV) Barrier Options");

        group.MapPost("/analyze", (FxLsvBarrierTelemetry telemetry, IFxLsvBarrierEngine engine) =>
        {
            var evaluation = engine.AnalyzeBarrierOption(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeFxLsvBarrier")
        .WithSummary("Analyzes foreign exchange (FX) local-stochastic volatility (LSV) surfaces, leverage function calibration, and exotic barrier option pricing");

        return endpoints;
    }
}
