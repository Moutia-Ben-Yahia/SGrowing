using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.EpigeneticMethylationClock;

public static class EpigeneticClockEndpoints
{
    public static IServiceCollection AddEpigeneticMethylationClockServices(this IServiceCollection services)
    {
        services.AddScoped<IEpigeneticMethylationClockEngine, EpigeneticMethylationClockEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEpigeneticClockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/epigenetic-clock")
            .WithTags("BioTech - Single-Cell Epigenetic DNA Methylation & HDAC Aging Clock");

        group.MapPost("/analyze-clock", (
            EpigeneticClockTelemetry telemetry,
            IEpigeneticMethylationClockEngine engine) =>
        {
            var result = engine.AnalyzeEpigeneticClock(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeEpigeneticClock")
        .WithSummary("Analyzes single-cell DNA CpG methylation, Horvath biological age divergence, LINE-1 retrotransposon integrity, and HDAC intervention.");

        return app;
    }
}
