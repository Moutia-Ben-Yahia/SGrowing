using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.IvtMrnaCappingPolyATail;

public static class IvtMrnaEndpoints
{
    public static IServiceCollection AddIvtMrnaCappingPolyAServices(this IServiceCollection services)
    {
        services.AddScoped<IIvtMrnaCappingPolyATailEngine, IvtMrnaCappingPolyATailEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapIvtMrnaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/ivt-mrna-capping")
            .WithTags("BioTech - IVT mRNA Capping & Poly(A) Tail Heterogeneity");

        group.MapPost("/analyze-batch", (
            IvtMrnaTelemetry telemetry,
            IIvtMrnaCappingPolyATailEngine engine) =>
        {
            var result = engine.AnalyzeIvtMrnaBatch(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeIvtMrnaBatch")
        .WithSummary("Analyzes in vitro transcription (IVT) mRNA capping efficiency, dsRNA contamination, poly(A) tail length, and immunogenicity.");

        return app;
    }
}
