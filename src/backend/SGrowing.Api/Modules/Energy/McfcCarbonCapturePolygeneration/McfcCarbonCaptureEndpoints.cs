using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.McfcCarbonCapturePolygeneration;

public static class McfcCarbonCaptureEndpoints
{
    public static IServiceCollection AddMcfcCarbonCaptureServices(this IServiceCollection services)
    {
        services.AddScoped<IMcfcCarbonCapturePolygenerationEngine, McfcCarbonCapturePolygenerationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMcfcCarbonCaptureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/mcfc-carbon-capture")
            .WithTags("Energy - Molten Carbonate Fuel Cell (MCFC) Carbon Capture");

        group.MapPost("/analyze-stack", (
            McfcCaptureTelemetry telemetry,
            IMcfcCarbonCapturePolygenerationEngine engine) =>
        {
            var result = engine.AnalyzeCarbonCaptureCell(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMcfcStack")
        .WithSummary("Analyzes high-temperature molten carbonate fuel cell (MCFC) industrial carbon capture, carbonate flux, and polygeneration.");

        return app;
    }
}
