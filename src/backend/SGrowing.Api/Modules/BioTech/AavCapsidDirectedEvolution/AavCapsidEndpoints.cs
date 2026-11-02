using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.AavCapsidDirectedEvolution;

public static class AavCapsidEndpoints
{
    public static IServiceCollection AddAavCapsidDirectedEvolutionServices(this IServiceCollection services)
    {
        services.AddScoped<IAavCapsidDirectedEvolutionEngine, AavCapsidDirectedEvolutionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAavCapsidEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/aav-capsid-directed-evolution")
            .WithTags("BioTech AAV Capsid Directed Evolution");

        group.MapPost("/analyze", (AavCapsidTelemetry telemetry, IAavCapsidDirectedEvolutionEngine engine) =>
        {
            var result = engine.AnalyzeCapsidEvolution(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAavCapsidDirectedEvolution")
        .WithSummary("Analyzes synthetic AAV capsid library variants for target organ bioavailability, hepatic de-targeting, and NAb neutralization escape.");

        return endpoints;
    }
}
