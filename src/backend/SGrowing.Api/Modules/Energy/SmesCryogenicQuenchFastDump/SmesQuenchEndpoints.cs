using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.SmesCryogenicQuenchFastDump;

public static class SmesQuenchEndpoints
{
    public static IServiceCollection AddSmesQuenchServices(this IServiceCollection services)
    {
        services.AddScoped<ISmesCryogenicQuenchFastDumpEngine, SmesCryogenicQuenchFastDumpEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSmesQuenchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/smes-quench")
            .WithTags("Energy - SMES Cryogenic Quench Fast Dump");

        group.MapPost("/analyze", (
            SmesQuenchTelemetry telemetry,
            ISmesCryogenicQuenchFastDumpEngine engine) =>
        {
            var result = engine.AnalyzeQuenchState(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSmesQuenchState")
        .WithSummary("Analyzes HTS SMES co-wound resistive voltage, hot spot growth, and pyrotechnic fast dump triggers.");

        return app;
    }
}
