using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.NanoporeSquiggleBasecalling;

public static class NanoporeSquiggleEndpoints
{
    public static IServiceCollection AddNanoporeSquiggleServices(this IServiceCollection services)
    {
        services.AddScoped<INanoporeSquiggleEngine, NanoporeSquiggleEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNanoporeSquiggleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/nanopore-squiggle-basecalling")
            .WithTags("BioTech Nanopore Squiggle Basecalling & Epigenetics");

        group.MapPost("/analyze", (NanoporeSquiggleTelemetry telemetry, INanoporeSquiggleEngine engine) =>
        {
            var evaluation = engine.AnalyzeSquiggle(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeNanoporeSquiggle")
        .WithSummary("Analyzes Oxford Nanopore raw electrical current squiggles, helicase stepping processivity, and direct 5mC epigenetic basecalling");

        return endpoints;
    }
}
