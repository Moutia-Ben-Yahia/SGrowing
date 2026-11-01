using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Tenancy.MultiRegion;

public static class MultiRegionEndpoints
{
    public static IServiceCollection AddMultiRegionSovereignServices(this IServiceCollection services)
    {
        services.AddSingleton<ISovereignDataShardingManager, SovereignDataShardingManager>();
        services.AddSingleton<IGeoRoutingMeshService, GeoRoutingMeshService>();
        services.AddSingleton<IHlcReplicationCoordinator, HlcReplicationCoordinator>();

        return services;
    }

    public static IEndpointRouteBuilder MapMultiRegionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenancy/multi-region")
            .WithTags("Global Multi-Region Active-Active Replication & Geo-Proximity Sharding");

        // 1. GET /shards: list regional shards and latency scores
        group.MapGet("/shards", (IGeoRoutingMeshService meshService) =>
        {
            var shards = meshService.GetAllShards();
            var activeCount = shards.Count(s => s.IsActive && s.HealthState == ShardHealthState.Healthy);
            var degradedCount = shards.Count(s => s.HealthState == ShardHealthState.Degraded || !s.IsActive);

            var response = new ShardStatusResponse(
                Shards: shards,
                ActiveShardCount: activeCount,
                DegradedShardCount: degradedCount,
                TimestampUtc: DateTime.UtcNow
            );

            return Results.Ok(response);
        })
        .WithName("GetRegionalShards")
        .WithSummary("Retrieve all global regional shards, replication latency, and health states");

        // 2. POST /route: compute geo-proximity routing decision
        group.MapPost("/route", (
            [FromBody] GeoRouteRequest request,
            IGeoRoutingMeshService meshService) =>
        {
            var decision = meshService.ResolveRoute(request);
            return Results.Ok(decision);
        })
        .WithName("ResolveGeoRoute")
        .WithSummary("Compute geo-proximity lowest-latency routing decision respecting sovereign boundaries");

        // 3. POST /policies: register tenant data residency sovereignty policy
        group.MapPost("/policies", async (
            [FromBody] RegisterResidencyPolicyRequest request,
            ISovereignDataShardingManager sovereignManager) =>
        {
            var policy = new TenantDataResidencyPolicy(
                TenantId: request.TenantId,
                HomeRegion: request.HomeRegion,
                EnforcedJurisdiction: request.EnforcedJurisdiction,
                AllowCrossRegionReadReplicas: request.AllowCrossRegionReadReplicas,
                DataLocalityStrictness: request.DataLocalityStrictness,
                ConfiguredAtUtc: DateTime.UtcNow,
                ConfiguredBy: request.ConfiguredBy ?? "Admin"
            );

            await sovereignManager.RegisterResidencyPolicyAsync(policy);
            return Results.Created($"/api/v1/tenancy/multi-region/policies/{policy.TenantId}", policy);
        })
        .WithName("RegisterResidencyPolicy")
        .WithSummary("Register or update tenant data sovereignty and residency localization policy");

        // 4. POST /replicate: execute cross-region HLC transaction replication
        group.MapPost("/replicate", async (
            [FromBody] ReplicationSyncRequest request,
            IHlcReplicationCoordinator replicationCoordinator) =>
        {
            var result = await replicationCoordinator.ReplicateTransactionAsync(request);
            return Results.Ok(result);
        })
        .WithName("ReplicateTransaction")
        .WithSummary("Execute active-active cross-region transaction replication with HLC conflict resolution");

        // 5. GET /health: multi-region engine health and telemetry
        group.MapGet("/health", (
            IGeoRoutingMeshService meshService,
            IHlcReplicationCoordinator replicationCoordinator,
            ISovereignDataShardingManager sovereignManager) =>
        {
            var shards = meshService.GetAllShards();
            var violations = sovereignManager.GetViolationAuditLogs();

            var metrics = new Dictionary<string, string>
            {
                ["TotalShards"] = shards.Count.ToString(),
                ["ActiveShards"] = shards.Count(s => s.IsActive).ToString(),
                ["SovereignViolationsLogged"] = violations.Count.ToString(),
                ["NodeAlgiersHlc"] = replicationCoordinator.GetCurrentTime("node-algiers-01").ToString(),
                ["NodeParisHlc"] = replicationCoordinator.GetCurrentTime("node-paris-01").ToString(),
                ["NodeDubaiHlc"] = replicationCoordinator.GetCurrentTime("node-dubai-01").ToString()
            };

            var health = new EngineHealthResponse(
                Status: "Healthy",
                Version: "2.0-net10-sovereign",
                Metrics: metrics,
                TimestampUtc: DateTime.UtcNow
            );

            return Results.Ok(health);
        })
        .WithName("GetMultiRegionEngineHealth")
        .WithSummary("Retrieve engine health status and multi-region telemetry metrics");

        // 6. GET /violations: audit log of sovereign data egress violations
        group.MapGet("/violations", (
            [FromQuery] Guid? tenantId,
            ISovereignDataShardingManager sovereignManager) =>
        {
            var violations = sovereignManager.GetViolationAuditLogs(tenantId);
            return Results.Ok(violations);
        })
        .WithName("GetSovereignViolations")
        .WithSummary("Retrieve audit logs of attempted cross-border sovereignty violations");

        return app;
    }
}
