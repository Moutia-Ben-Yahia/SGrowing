using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.PhageAssistedContinuousEvolution;

public static class PaceEndpoints
{
    public static IServiceCollection AddPaceServices(this IServiceCollection services)
    {
        services.AddScoped<IPhageAssistedContinuousEvolutionEngine, PhageAssistedContinuousEvolutionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/pace-evolution")
            .WithTags("BioTech - Phage-Assisted Continuous Evolution (PACE)");

        group.MapPost("/analyze-evolution", (
            PaceTelemetry telemetry,
            IPhageAssistedContinuousEvolutionEngine engine) =>
        {
            var result = engine.AnalyzeContinuousEvolution(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzePaceEvolution")
        .WithSummary("Analyzes phage-assisted continuous directed evolution, lagoon dilution vs replication kinetics, mutagenesis plasmid induction, and selection drive.");

        return app;
    }
}
