using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.TargetedAlphaEmitterDosimetry;

public static class TargetedAlphaEmitterEndpoints
{
    public static IServiceCollection AddTargetedAlphaDosimetryServices(this IServiceCollection services)
    {
        services.AddScoped<ITargetedAlphaEmitterDosimetryEngine, TargetedAlphaEmitterDosimetryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTargetedAlphaDosimetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/alpha-dosimetry")
            .WithTags("BioTech - Targeted Alpha Emitter Microdosimetry");

        group.MapPost("/analyze-plan", (
            TargetedAlphaDosimetryTelemetry telemetry,
            ITargetedAlphaEmitterDosimetryEngine engine) =>
        {
            var result = engine.AnalyzeAlphaMicrodosimetry(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAlphaDosimetry")
        .WithSummary("Analyzes targeted alpha therapy (TAT) microdosimetry, tumor lethal track dose, daughter recoil, and renal clearance.");

        return app;
    }
}
