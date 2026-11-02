using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SupercriticalThermosiphonGeothermal;

public static class SupercriticalThermosiphonEndpoints
{
    public static IServiceCollection AddSupercriticalThermosiphonServices(this IServiceCollection services)
    {
        services.AddScoped<ISupercriticalThermosiphonGeothermalEngine, SupercriticalThermosiphonGeothermalEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSupercriticalThermosiphonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/supercritical-thermosiphon")
            .WithTags("Energy - Supercritical Closed-Loop Thermosiphon Geothermal");

        group.MapPost("/analyze-wellbore", (
            SupercriticalThermosiphonTelemetry telemetry,
            ISupercriticalThermosiphonGeothermalEngine engine) =>
        {
            var result = engine.AnalyzeThermosiphonState(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeThermosiphonWellbore")
        .WithSummary("Analyzes deep geothermal closed-loop supercritical sCO2 thermosiphon natural buoyancy drive and exergy extraction.");

        return app;
    }
}
