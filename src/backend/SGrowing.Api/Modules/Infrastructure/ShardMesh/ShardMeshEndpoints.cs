using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.ShardMesh;

public static class ShardMeshEndpoints
{
    public static IServiceCollection AddShardMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<IShardMeshEngine, ShardMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapShardMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/shard-mesh")
            .WithTags("Infrastructure - Database Shard Mesh");

        group.MapGet("/topology", (IShardMeshEngine engine) =>
        {
            var topology = engine.GetTopology();
            return Results.Ok(topology);
        })
        .WithName("GetShardMeshTopology")
        .WithSummary("Retrieves multi-region active-active database shard cluster status and load");

        group.MapGet("/route/{tenantId}", (string tenantId, IShardMeshEngine engine) =>
        {
            var routing = engine.RouteTenant(tenantId);
            return Results.Ok(routing);
        })
        .WithName("RouteTenantShard")
        .WithSummary("Routes tenant ID to deterministic virtual hash bucket and host shard endpoint");

        group.MapPost("/rebalance", (RebalanceShardRequest request, IShardMeshEngine engine) =>
        {
            var result = engine.RebalanceBuckets(request);
            return Results.Ok(result);
        })
        .WithName("RebalanceShardBuckets")
        .WithSummary("Rebalances virtual hash buckets across regional shard nodes without downtime");

        return endpoints;
    }
}
