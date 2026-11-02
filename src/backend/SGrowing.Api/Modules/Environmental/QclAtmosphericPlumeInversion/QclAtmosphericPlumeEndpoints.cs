using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Environmental.QclAtmosphericPlumeInversion;

public static class QclAtmosphericPlumeEndpoints
{
    public static IServiceCollection AddQclAtmosphericPlumeServices(this IServiceCollection services)
    {
        services.AddScoped<IQclAtmosphericPlumeInversionEngine, QclAtmosphericPlumeInversionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapQclAtmosphericPlumeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/environmental/qcl-plume")
            .WithTags("Environmental - QCL Mid-IR Open-Path Plume Inversion");

        group.MapPost("/analyze-plume", (
            QclPlumeTelemetry telemetry,
            IQclAtmosphericPlumeInversionEngine engine) =>
        {
            var result = engine.AnalyzeGreenhousePlume(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeGreenhousePlume")
        .WithSummary("Analyzes mid-infrared quantum cascade laser (QCL) open-path absorption, Gaussian plume inversion, and fugitive leak fluxes.");

        return app;
    }
}
