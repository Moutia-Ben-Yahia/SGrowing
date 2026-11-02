using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.Sco2BraytonCompressorSurge;

public static class Sco2BraytonEndpoints
{
    public static IServiceCollection AddSco2BraytonServices(this IServiceCollection services)
    {
        services.AddScoped<ISco2BraytonCompressorSurgeEngine, Sco2BraytonCompressorSurgeEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSco2BraytonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/sco2-brayton")
            .WithTags("Energy - sCO2 Brayton Compressor Surge");

        group.MapPost("/analyze", (
            Sco2CompressorTelemetry telemetry,
            ISco2BraytonCompressorSurgeEngine engine) =>
        {
            var result = engine.AnalyzeCompressorSurge(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSco2CompressorSurge")
        .WithSummary("Analyzes near-critical sCO2 compressor surge margin and Greitzer B parameter for anti-surge control.");

        return app;
    }
}
