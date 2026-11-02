using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CellFreeProteinSynthesisExpression;

public static class CellFreeProteinSynthesisEndpoints
{
    public static IServiceCollection AddCellFreeProteinSynthesisServices(this IServiceCollection services)
    {
        services.AddScoped<ICellFreeProteinSynthesisEngine, CellFreeProteinSynthesisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCellFreeProteinSynthesisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/cell-free-protein-synthesis")
            .WithTags("BioTech Cell-Free Protein Synthesis");

        group.MapPost("/analyze", (CellFreeProteinSynthesisTelemetry telemetry, ICellFreeProteinSynthesisEngine engine) =>
        {
            var result = engine.AnalyzeSynthesis(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCellFreeProteinSynthesis")
        .WithSummary("Analyzes cell-free protein synthesis (CFPS) linear DNA degradation resistance with GamS inhibitor, 5' RBS secondary hairpins, and ribosome codon stalling.");

        return endpoints;
    }
}
