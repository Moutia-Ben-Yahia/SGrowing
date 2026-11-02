using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.SviVolatilitySurfaceCalibration;

public static class SviVolatilityEndpoints
{
    public static IServiceCollection AddSviVolatilityServices(this IServiceCollection services)
    {
        services.AddScoped<ISviVolatilityEngine, SviVolatilityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSviVolatilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/svi-volatility-surface-calibration")
            .WithTags("Finance Stochastic Volatility Inspired (SVI) & Arbitrage-Free Surfaces");

        group.MapPost("/analyze", (SviVolatilityTelemetry telemetry, ISviVolatilityEngine engine) =>
        {
            var evaluation = engine.AnalyzeSurface(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSviVolatilitySurface")
        .WithSummary("Analyzes SVI parameterizations, asymptotic wing slopes, butterfly arbitrage density, and calendar spread monotonicity");

        return endpoints;
    }
}
