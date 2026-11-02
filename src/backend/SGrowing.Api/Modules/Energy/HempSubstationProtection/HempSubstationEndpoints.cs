using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.HempSubstationProtection;

public static class HempSubstationEndpoints
{
    public static IServiceCollection AddHempSubstationServices(this IServiceCollection services)
    {
        services.AddScoped<IHempSubstationProtectionEngine, HempSubstationProtectionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHempSubstationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/hemp-substation")
            .WithTags("Energy - HEMP Substation Fast-Interception");

        group.MapPost("/analyze-threat", (
            SubstationHempTelemetry telemetry,
            IHempSubstationProtectionEngine engine) =>
        {
            var result = engine.AnalyzeHempThreat(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHempThreat")
        .WithSummary("Analyzes HEMP E1/E2 nanosecond surge clamping, E3 geomagnetically induced currents, and transformer DC blocking.");

        return app;
    }
}
