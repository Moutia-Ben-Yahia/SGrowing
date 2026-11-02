using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.HaleSolarEnergyBalance;

public static class HaleSolarEndpoints
{
    public static IServiceCollection AddHaleSolarEnergyServices(this IServiceCollection services)
    {
        services.AddScoped<IHaleSolarEnergyBalanceEngine, HaleSolarEnergyBalanceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHaleSolarEnergyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/hale-solar")
            .WithTags("Aerospace - Stratospheric HALE Solar-Electric Energy Balance");

        group.MapPost("/analyze-balance", (
            HaleSolarTelemetry telemetry,
            IHaleSolarEnergyBalanceEngine engine) =>
        {
            var result = engine.AnalyzeDiurnalEnergyBalance(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHaleSolarBalance")
        .WithSummary("Analyzes stratospheric pseudo-satellite diurnal solar energy balance, gravitational climb buffering, and night survival SOC.");

        return app;
    }
}
