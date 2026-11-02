using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CrisprBaseEditorDeaminaseBystander;

public static class CrisprBaseEditorEndpoints
{
    public static IServiceCollection AddCrisprBaseEditorServices(this IServiceCollection services)
    {
        services.AddScoped<ICrisprBaseEditorDeaminaseBystanderEngine, CrisprBaseEditorDeaminaseBystanderEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrisprBaseEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/crispr-base-editor")
            .WithTags("BioTech - CRISPR Base Editing Deaminase Fidelity");

        group.MapPost("/analyze-fidelity", (
            CrisprBaseEditorTelemetry telemetry,
            ICrisprBaseEditorDeaminaseBystanderEngine engine) =>
        {
            var result = engine.AnalyzeBaseEditingFidelity(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeBaseEditorFidelity")
        .WithSummary("Analyzes CRISPR cytidine/adenine base editing catalytic window, bystander mutagenesis, RNA off-target activity, and indel suppression.");

        return app;
    }
}
