using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.GuideSeqPrimeEditingOffTarget;

public static class GuideSeqEditingEndpoints
{
    public static IServiceCollection AddGuideSeqEditingServices(this IServiceCollection services)
    {
        services.AddScoped<IGuideSeqEditingEngine, GuideSeqEditingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGuideSeqEditingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/guide-seq-editing-off-target")
            .WithTags("BioTech GUIDE-Seq Base & Prime Editing Off-Target Profiling");

        group.MapPost("/analyze", (GuideSeqEditingTelemetry telemetry, IGuideSeqEditingEngine engine) =>
        {
            var evaluation = engine.AnalyzeAssay(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeGuideSeqEditing")
        .WithSummary("Analyzes GUIDE-seq genome-wide off-target cleavage, base/prime editing precision, edit:indel ratios, and transcriptome deamination");

        return endpoints;
    }
}
