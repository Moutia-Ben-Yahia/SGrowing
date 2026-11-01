using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.GraphFraud;

public static class IdentityGraphFraudEndpoints
{
    public static IServiceCollection AddIdentityGraphFraudServices(this IServiceCollection services)
    {
        services.AddSingleton<IIdentityGraphEngine, IdentityGraphEngine>();
        services.AddSingleton<IGraphClusterAnalyzerService, GraphClusterAnalyzerService>();
        services.AddSingleton<IFraudRingContainmentService, FraudRingContainmentService>();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityGraphFraudEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/graph-fraud")
            .WithTags("Graph-Powered Fraud Ring Detection, Identity Resolution & Topological Clustering");

        // 1. Link customer entities into graph
        group.MapPost("/link", (
            [FromBody] LinkCustomerEntitiesRequest request,
            [FromServices] IIdentityGraphEngine engine) =>
        {
            if (string.IsNullOrWhiteSpace(request.CustomerId))
            {
                return Results.BadRequest(new { error = "CustomerId is required." });
            }

            var response = engine.LinkCustomerEntities(request);
            return Results.Ok(response);
        })
        .WithName("LinkCustomerEntities")
        .WithSummary("Ingest customer and linked behavioral / physical entities into the identity graph.");

        // 2. Discover and retrieve detected fraud clusters
        group.MapGet("/clusters", (
            [FromQuery] double? minRiskScore,
            [FromQuery] int? limit,
            [FromServices] IGraphClusterAnalyzerService analyzer) =>
        {
            var clusters = analyzer.DetectFraudClusters(
                minRiskScore: minRiskScore ?? 0.0,
                limit: Math.Clamp(limit ?? 50, 1, 200));

            return Results.Ok(clusters);
        })
        .WithName("GetFraudClusters")
        .WithSummary("Detect and return topological fraud clusters and collusive rings.");

        // 3. Get single fraud cluster details
        group.MapGet("/clusters/{clusterId}", (
            [FromRoute] string clusterId,
            [FromServices] IGraphClusterAnalyzerService analyzer) =>
        {
            var cluster = analyzer.GetClusterById(clusterId);
            return cluster is not null ? Results.Ok(cluster) : Results.NotFound(new { error = $"Cluster {clusterId} not found." });
        })
        .WithName("GetFraudClusterById")
        .WithSummary("Retrieve details, risk metrics, and member nodes for a specific fraud cluster.");

        // 4. Query node neighborhood (1-hop to 3-hops subgraph)
        group.MapGet("/nodes/{nodeId}/neighborhood", (
            [FromRoute] string nodeId,
            [FromQuery] int? depth,
            [FromServices] IIdentityGraphEngine engine) =>
        {
            var effectiveDepth = Math.Clamp(depth ?? 1, 1, 3);
            var (nodes, edges) = engine.GetSubGraph(nodeId, effectiveDepth);

            if (nodes.Count == 0)
            {
                return Results.NotFound(new { error = $"Node {nodeId} not found in graph." });
            }

            var response = new NeighborhoodResponse
            {
                CenterNodeId = nodeId,
                Depth = effectiveDepth,
                Nodes = nodes,
                Edges = edges
            };

            return Results.Ok(response);
        })
        .WithName("GetNodeNeighborhood")
        .WithSummary("Retrieve the ego-network neighborhood subgraph around an identity node.");

        // 5. Query node centrality & PageRank
        group.MapGet("/nodes/{nodeId}/centrality", (
            [FromRoute] string nodeId,
            [FromServices] IGraphClusterAnalyzerService analyzer) =>
        {
            var metric = analyzer.GetNodeCentrality(nodeId);
            return metric is not null ? Results.Ok(metric) : Results.NotFound(new { error = $"Node {nodeId} not found." });
        })
        .WithName("GetNodeCentrality")
        .WithSummary("Retrieve centrality, PageRank, and bridge/mule classification for an entity node.");

        // 6. Detect high-degree mule & bridge nodes
        group.MapGet("/mules", (
            [FromQuery] int? minConnectedCustomers,
            [FromServices] IGraphClusterAnalyzerService analyzer) =>
        {
            var muleNodeIds = analyzer.DetectMuleNodes(minConnectedCustomers ?? 3);
            return Results.Ok(muleNodeIds);
        })
        .WithName("GetDetectedMules")
        .WithSummary("Identify infrastructure nodes (devices, RIBs, phones) shared across multiple customer accounts.");

        // 7. 1-Click ring quarantine and containment
        group.MapPost("/clusters/{clusterId}/quarantine", async (
            [FromRoute] string clusterId,
            [FromBody] QuarantineClusterRequest? request,
            [FromServices] IFraudRingContainmentService containment,
            CancellationToken ct) =>
        {
            var action = await containment.QuarantineClusterAsync(
                clusterId,
                request?.Reason,
                request?.OperatorId,
                ct);

            var response = new QuarantineClusterResponse
            {
                ClusterId = clusterId,
                Success = action.QuarantinedNodesCount > 0,
                ActionSummary = action,
                QuarantinedAtUtc = DateTime.UtcNow
            };

            return Results.Ok(response);
        })
        .WithName("QuarantineFraudCluster")
        .WithSummary("Trigger 1-click containment: freeze cluster nodes, active orders, and virtual escrow balances.");

        // 8. Containment audit ledger
        group.MapGet("/containment/audit", (
            [FromServices] IFraudRingContainmentService containment) =>
        {
            var trail = containment.GetAuditTrail();
            return Results.Ok(trail);
        })
        .WithName("GetContainmentAuditTrail")
        .WithSummary("Retrieve historical record of all executed cluster quarantine and containment operations.");

        // 9. Graph fraud engine health
        group.MapGet("/health", (
            [FromServices] IIdentityGraphEngine engine,
            [FromServices] IGraphClusterAnalyzerService analyzer) =>
        {
            var allNodes = engine.GetAllNodes();
            var quarantinedCount = allNodes.Count(n => n.IsQuarantined);
            var clusters = analyzer.DetectFraudClusters();

            var health = new GraphFraudEngineHealthResponse
            {
                IsHealthy = true,
                TotalNodes = engine.NodeCount,
                TotalEdges = engine.EdgeCount,
                QuarantinedNodesCount = quarantinedCount,
                DetectedClustersCount = clusters.Count,
                ServerTimeUtc = DateTime.UtcNow
            };

            return Results.Ok(health);
        })
        .WithName("GetGraphFraudEngineHealth")
        .WithSummary("Retrieve health metrics and statistics for the graph fraud detection engine.");

        return app;
    }
}
