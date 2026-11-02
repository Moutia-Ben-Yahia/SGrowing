using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.SabrVolatilitySurfaceCalibration;

public static class SabrVolatilityEndpoints
{
    public static IServiceCollection AddSabrVolatilitySurfaceCalibrationServices(this IServiceCollection services)
    {
        services.AddScoped<ISabrVolatilitySurfaceCalibrationEngine, SabrVolatilitySurfaceCalibrationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSabrVolatilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/sabr-volatility-surface")
            .WithTags("Finance SABR Implied Volatility Surface");

        group.MapPost("/analyze", (SabrVolatilityTelemetry telemetry, ISabrVolatilitySurfaceCalibrationEngine engine) =>
        {
            var result = engine.AnalyzeSabrSurface(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSabrVolatilitySurface")
        .WithSummary("Analyzes Hagan SABR stochastic volatility smile calibration, ATM skew slope, and butterfly spread arbitrage-free density constraints.");

        return endpoints;
    }
}
