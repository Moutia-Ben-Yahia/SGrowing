using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.DisasterRecovery;

public static class DisasterRecoveryEndpoints
{
    public static IServiceCollection AddDisasterRecoveryServices(this IServiceCollection services)
    {
        services.AddSingleton<IDisasterRecoveryEngine, DisasterRecoveryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDisasterRecoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/disaster-recovery")
            .WithTags("Infrastructure - Multi-Cloud Disaster Recovery");

        group.MapGet("/topology", (IDisasterRecoveryEngine engine) =>
        {
            var topology = engine.GetTopology();
            return Results.Ok(topology);
        })
        .WithName("GetDisasterRecoveryTopology")
        .WithSummary("Get cross-cloud cluster health, replication lag, and quorum status");

        group.MapPost("/simulate-outage", (SimulateOutageRequest request, IDisasterRecoveryEngine engine) =>
        {
            var updated = engine.SimulateOutage(request);
            return Results.Ok(updated);
        })
        .WithName("SimulateDatacenterOutage")
        .WithSummary("Simulate node outage or network partition to test quorum resiliency");

        group.MapPost("/promote/{targetNodeId}", (string targetNodeId, IDisasterRecoveryEngine engine) =>
        {
            var result = engine.PromoteHotStandby(targetNodeId);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("PromoteStandbyToPrimary")
        .WithSummary("Promote hot-standby node to PrimaryActive with sub-30s DNS cutover");

        return endpoints;
    }
}
