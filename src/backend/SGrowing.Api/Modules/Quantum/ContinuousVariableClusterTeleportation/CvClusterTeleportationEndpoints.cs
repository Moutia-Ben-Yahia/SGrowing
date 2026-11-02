using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.ContinuousVariableClusterTeleportation;

public static class CvClusterTeleportationEndpoints
{
    public static IServiceCollection AddCvClusterTeleportationServices(this IServiceCollection services)
    {
        services.AddScoped<ICvClusterTeleportationEngine, CvClusterTeleportationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCvClusterTeleportationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/cv-cluster-teleportation")
            .WithTags("Quantum Continuous-Variable Cluster State & Squeezed Light Teleportation");

        group.MapPost("/analyze", (CvClusterTeleportationTelemetry telemetry, ICvClusterTeleportationEngine engine) =>
        {
            var evaluation = engine.AnalyzeCluster(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeCvClusterTeleportation")
        .WithSummary("Analyzes continuous-variable optical cluster states, squeezing parameter r, homodyne quantum efficiencies, and teleportation fidelity");

        return endpoints;
    }
}
