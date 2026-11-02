using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CellFreeProteinTranslation;

public static class CellFreeProteinEndpoints
{
    public static IServiceCollection AddCellFreeProteinServices(this IServiceCollection services)
    {
        services.AddScoped<ICellFreeProteinTranslationEngine, CellFreeProteinTranslationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCellFreeProteinEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/cell-free-protein")
            .WithTags("BioTech - Cell-Free Protein Synthesis TX-TL");

        group.MapPost("/analyze", (
            CellFreeBioreactorTelemetry telemetry,
            ICellFreeProteinTranslationEngine engine) =>
        {
            var result = engine.AnalyzeBioreactorKinetics(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCellFreeBioreactor")
        .WithSummary("Analyzes in-vitro cell-free protein synthesis ribosomal elongation rates and energy regeneration.");

        return app;
    }
}
