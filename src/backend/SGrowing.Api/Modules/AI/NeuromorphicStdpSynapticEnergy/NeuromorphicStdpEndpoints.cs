using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.NeuromorphicStdpSynapticEnergy;

public static class NeuromorphicStdpEndpoints
{
    public static IServiceCollection AddNeuromorphicStdpServices(this IServiceCollection services)
    {
        services.AddScoped<INeuromorphicStdpSynapticEnergyEngine, NeuromorphicStdpSynapticEnergyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNeuromorphicStdpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/neuromorphic-stdp")
            .WithTags("AI - Neuromorphic SNN STDP Synaptic Energy");

        group.MapPost("/analyze-core", (
            NeuromorphicCoreTelemetry telemetry,
            INeuromorphicStdpSynapticEnergyEngine engine) =>
        {
            var result = engine.AnalyzeNeuromorphicCore(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeNeuromorphicCore")
        .WithSummary("Analyzes spiking neural network STDP synaptic plasticity, femtojoule energy dissipation, and burst synchrony.");

        return app;
    }
}
