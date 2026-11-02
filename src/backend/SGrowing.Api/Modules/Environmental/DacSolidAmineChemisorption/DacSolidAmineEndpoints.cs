using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Environmental.DacSolidAmineChemisorption;

public static class DacSolidAmineEndpoints
{
    public static IServiceCollection AddDacSolidAmineServices(this IServiceCollection services)
    {
        services.AddScoped<IDacSolidAmineChemisorptionEngine, DacSolidAmineChemisorptionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDacSolidAmineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/environmental/dac-chemisorption")
            .WithTags("Environmental - Direct Air Capture Solid-Amine Chemisorption");

        group.MapPost("/analyze", (
            DacContactorTelemetry telemetry,
            IDacSolidAmineChemisorptionEngine engine) =>
        {
            var result = engine.AnalyzeContactorCycle(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDacContactorCycle")
        .WithSummary("Analyzes solid-amine direct air capture CO2 loading, water competition, and TVSA desorption energy.");

        return app;
    }
}
