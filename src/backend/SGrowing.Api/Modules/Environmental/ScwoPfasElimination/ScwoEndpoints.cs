using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Environmental.ScwoPfasElimination;

public static class ScwoEndpoints
{
    public static IServiceCollection AddScwoPfasEliminationServices(this IServiceCollection services)
    {
        services.AddScoped<IScwoPfasEliminationEngine, ScwoPfasEliminationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapScwoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/environmental/scwo-pfas-elimination")
            .WithTags("Environmental - Supercritical Water Oxidation (SCWO) PFAS Elimination");

        group.MapPost("/analyze-process", (
            ScwoTelemetry telemetry,
            IScwoPfasEliminationEngine engine) =>
        {
            var result = engine.AnalyzeScwoProcess(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeScwoPfasElimination")
        .WithSummary("Analyzes supercritical water oxidation (SCWO), refractory PFAS defluorination efficiency, hydrothermal salt plugging, and mineralization.");

        return app;
    }
}
