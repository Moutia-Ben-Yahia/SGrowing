using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.RareEarthOpticalMemory;

public static class RareEarthMemoryEndpoints
{
    public static IServiceCollection AddRareEarthMemoryServices(this IServiceCollection services)
    {
        services.AddScoped<IRareEarthMemoryEngine, RareEarthMemoryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRareEarthMemoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/rare-earth-optical-memory")
            .WithTags("Quantum Rare-Earth Optical Memory");

        group.MapPost("/analyze", (RareEarthMemoryTelemetry telemetry, IRareEarthMemoryEngine engine) =>
        {
            var evaluation = engine.AnalyzeQuantumMemory(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeRareEarthMemory")
        .WithSummary("Analyzes cryogenic solid-state rare-earth quantum optical memory, AFC efficiency, and spin-wave retention");

        return endpoints;
    }
}
