using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sustainability.CircularRemanufacturing;

public static class CircularRemanufacturingEndpoints
{
    public static IServiceCollection AddCircularRemanufacturingServices(this IServiceCollection services)
    {
        services.AddSingleton<DisassemblyYieldOptimizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCircularRemanufacturingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sustainability/circular-remanufacturing")
            .WithTags("Autonomous Circular Economy Reverse Remanufacturing & Material Disassembly Yield Forecaster");

        group.MapPost("/analyze-yield", (
            AnalyzeProductYieldRequest request,
            DisassemblyYieldOptimizationEngine engine) =>
        {
            var report = engine.AnalyzeYield(request);
            return Results.Ok(report);
        });

        return app;
    }
}
