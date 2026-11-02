using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CrisprCas12aCollateralCleavage;

public static class CrisprCas12aEndpoints
{
    public static IServiceCollection AddCrisprCas12aServices(this IServiceCollection services)
    {
        services.AddScoped<ICrisprCas12aCollateralCleavageEngine, CrisprCas12aCollateralCleavageEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrisprCas12aEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/cas12a-detectr")
            .WithTags("BioTech - CRISPR Cas12a DETECTR Viral Diagnostics");

        group.MapPost("/analyze", (
            Cas12aAssayTelemetry telemetry,
            ICrisprCas12aCollateralCleavageEngine engine) =>
        {
            var result = engine.AnalyzeAssayDiagnostics(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCas12aAssay")
        .WithSummary("Analyzes CRISPR-Cas12a trans-cleavage kinetics and fluorescence SNR for rapid viral diagnostics.");

        return app;
    }
}
