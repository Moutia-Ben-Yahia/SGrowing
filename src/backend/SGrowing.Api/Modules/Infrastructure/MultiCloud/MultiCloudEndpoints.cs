using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.MultiCloud;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 45:
/// Global Sovereign Multi-Cloud Mesh, Anycast Disaster Recovery &amp; Database Split-Brain Arbitrator.
/// </summary>
public static class MultiCloudEndpoints
{
    /// <summary>
    /// Registers Phase 45 Multi-Cloud Mesh, Split-Brain Arbitrator, and Autonomous DR Autopilot services.
    /// </summary>
    public static IServiceCollection AddMultiCloudServices(this IServiceCollection services)
    {
        services.AddSingleton<IMultiCloudMeshGatewayService, MultiCloudMeshGatewayService>();
        services.AddSingleton<ISplitBrainQuorumArbitratorService, SplitBrainQuorumArbitratorService>();
        services.AddSingleton<IAutonomousDrAutopilotService, AutonomousDrAutopilotService>();

        return services;
    }

    /// <summary>
    /// Maps the Sovereign Multi-Cloud Mesh, Arbitrator, and DR Autopilot endpoints under /api/v1/infrastructure/multicloud.
    /// </summary>
    public static IEndpointRouteBuilder MapMultiCloudEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/multicloud")
            .WithTags("Global Sovereign Multi-Cloud Mesh & Disaster Recovery");

        // 1. GET /topology
        group.MapGet("/topology", ([FromServices] IMultiCloudMeshGatewayService gateway) =>
        {
            var topology = gateway.GetClusterTopology();
            return Results.Ok(topology);
        })
        .WithName("GetMultiCloudTopology")
        .WithSummary("Retrieves global multi-cloud mesh topology and active health status.");

        // 2. POST /nodes
        group.MapPost("/nodes", (
            [FromBody] RegisterNodeRequest request,
            [FromServices] IMultiCloudMeshGatewayService gateway) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.NodeId))
            {
                return Results.BadRequest(new { error = "Valid node registration payload with NodeId is required." });
            }

            var node = request.ToNode();
            gateway.RegisterNode(node);
            return Results.Created($"/api/v1/infrastructure/multicloud/nodes/{node.NodeId}", node);
        })
        .WithName("RegisterMultiCloudNode")
        .WithSummary("Registers or updates a region node in the multi-cloud mesh.");

        // 3. POST /nodes/{nodeId}/health
        group.MapPost("/nodes/{nodeId}/health", (
            [FromRoute] string nodeId,
            [FromBody] UpdateNodeHealthRequest request,
            [FromServices] IMultiCloudMeshGatewayService gateway) =>
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return Results.BadRequest(new { error = "Node ID path parameter is required." });
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "Health update payload is required." });
            }

            var existing = gateway.GetNode(nodeId);
            if (existing is null)
            {
                return Results.NotFound(new { error = $"Node '{nodeId}' not found in cluster." });
            }

            gateway.UpdateNodeHealth(nodeId, request.Status, request.LatencyMs);
            return Results.Ok(new
            {
                message = $"Node '{nodeId}' health updated successfully.",
                status = request.Status,
                latencyMs = request.LatencyMs
            });
        })
        .WithName("UpdateNodeHealth")
        .WithSummary("Updates region node operational health status and observed latency.");

        // 4. POST /quorum/heartbeat
        group.MapPost("/quorum/heartbeat", (
            [FromBody] NodeHeartbeatRequest request,
            [FromServices] ISplitBrainQuorumArbitratorService arbitrator) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.NodeId))
            {
                return Results.BadRequest(new { error = "Heartbeat payload with valid NodeId is required." });
            }

            bool accepted = arbitrator.ProcessHeartbeat(request.ToHeartbeat());
            return Results.Ok(new
            {
                accepted,
                currentTerm = arbitrator.CurrentTerm,
                currentLeader = arbitrator.CurrentLeaderId
            });
        })
        .WithName("SubmitQuorumHeartbeat")
        .WithSummary("Submits node heartbeat for Raft-inspired Byzantine consensus arbitration.");

        // 5. GET /quorum/status
        group.MapGet("/quorum/status", ([FromServices] ISplitBrainQuorumArbitratorService arbitrator) =>
        {
            var state = arbitrator.GetQuorumState();
            return Results.Ok(state);
        })
        .WithName("GetQuorumStatus")
        .WithSummary("Retrieves current distributed consensus quorum and lease state.");

        // 6. POST /dr/drill
        group.MapPost("/dr/drill", (
            [FromBody] TriggerDrDrillRequest request,
            [FromServices] IAutonomousDrAutopilotService autopilot) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TargetNodeId))
            {
                return Results.BadRequest(new { error = "Drill trigger payload with valid TargetNodeId is required." });
            }

            var report = autopilot.ExecuteDrill(request);
            return Results.Ok(report);
        })
        .WithName("TriggerDrDrill")
        .WithSummary("Triggers an automated non-destructive disaster recovery failover drill.");

        // 7. GET /dr/reports
        group.MapGet("/dr/reports", ([FromServices] IAutonomousDrAutopilotService autopilot) =>
        {
            var reports = autopilot.GetDrillHistory();
            return Results.Ok(reports);
        })
        .WithName("GetDrDrillReports")
        .WithSummary("Retrieves historical disaster recovery drill execution reports.");

        return app;
    }
}
