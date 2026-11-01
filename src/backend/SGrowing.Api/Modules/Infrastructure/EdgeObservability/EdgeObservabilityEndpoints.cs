using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.EdgeObservability;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Horizon 55:
/// Self-Healing Kubernetes / Nomad Edge Cluster Autopilot &amp; eBPF Kernel Mesh Observability.
/// </summary>
public static class EdgeObservabilityEndpoints
{
    /// <summary>
    /// Registers the Edge Cluster Autopilot, eBPF Kernel Socket Observability Engine,
    /// and Adaptive Load-Shedder Sentinel services.
    /// </summary>
    public static IServiceCollection AddEdgeObservabilityServices(this IServiceCollection services)
    {
        services.AddSingleton<IEdgeClusterAutopilotService, EdgeClusterAutopilotService>();
        services.AddSingleton<IEbpfKernelSocketObservabilityEngine, EbpfKernelSocketObservabilityEngine>();
        services.AddSingleton<IAdaptiveLoadShedderSentinelService, AdaptiveLoadShedderSentinelService>();

        return services;
    }

    /// <summary>
    /// Maps edge observability and autopilot endpoints under /api/v1/infrastructure/edge-observability.
    /// </summary>
    public static IEndpointRouteBuilder MapEdgeObservabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/edge-observability")
            .WithTags("Edge Cluster Autopilot & eBPF Kernel Mesh Observability");

        // 1. GET /cluster/health
        group.MapGet("/cluster/health", (
            [FromQuery] string? clusterId,
            [FromServices] IEdgeClusterAutopilotService autopilot) =>
        {
            var health = autopilot.GetClusterHealth(clusterId);
            return Results.Ok(health);
        })
        .WithName("GetEdgeClusterHealth")
        .WithSummary("Retrieves edge cluster health status, node states, and autonomous evacuation records.");

        // 2. POST /nodes/{nodeId}/drain
        group.MapPost("/nodes/{nodeId}/drain", (
            [FromRoute] string nodeId,
            [FromBody] DrainNodeRequest? request,
            [FromServices] IEdgeClusterAutopilotService autopilot) =>
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return Results.BadRequest(new { error = "Route parameter 'nodeId' cannot be empty." });
            }

            var response = autopilot.DrainNode(nodeId, request?.Reason, request?.ForceEvacuate ?? false);
            return response.Success
                ? Results.Ok(response)
                : Results.NotFound(response);
        })
        .WithName("DrainEdgeNode")
        .WithSummary("Cords and cleanly drains workloads from an edge compute node.");

        // 3. GET /ebpf/metrics
        group.MapGet("/ebpf/metrics", (
            [FromServices] IEbpfKernelSocketObservabilityEngine engine) =>
        {
            var summary = engine.GetMetricsSummary();
            return Results.Ok(summary);
        })
        .WithName("GetEbpfMetrics")
        .WithSummary("Retrieves kernel-level socket mesh metrics, RTT jitter, and micro-burst saturation alerts.");

        // 4. POST /load-shedding/override
        group.MapPost("/load-shedding/override", (
            [FromBody] LoadSheddingOverrideRequest request,
            [FromServices] IAdaptiveLoadShedderSentinelService loadShedder) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { error = "Request payload is required." });
            }

            if (request.ForceOverride)
            {
                loadShedder.SetManualOverride(true, request.ShedTiers, request.Reason);
            }
            else
            {
                loadShedder.ClearManualOverride();
            }

            var policy = loadShedder.EvaluateCurrentPolicy(0.0, 0.0);
            var response = new LoadSheddingOverrideResponse(
                Success: true,
                Message: request.ForceOverride
                    ? "Manual load shedding override applied successfully."
                    : "Manual load shedding override cleared.",
                ActivePolicy: policy);

            return Results.Ok(response);
        })
        .WithName("OverrideLoadShedding")
        .WithSummary("Applies or clears a manual load shedding tier override.");

        // 5. GET /load-shedding/status
        group.MapGet("/load-shedding/status", (
            [FromQuery] double? cpu,
            [FromQuery] double? packetLoss,
            [FromServices] IAdaptiveLoadShedderSentinelService loadShedder) =>
        {
            var status = loadShedder.GetStatus(cpu, packetLoss);
            return Results.Ok(status);
        })
        .WithName("GetLoadSheddingStatus")
        .WithSummary("Retrieves active load shedding tiers, hysteresis state, and drop percentage.");

        // Helper 6. POST /nodes/telemetry
        group.MapPost("/nodes/telemetry", (
            [FromBody] RegisterNodeTelemetryRequest request,
            [FromServices] IEdgeClusterAutopilotService autopilot) =>
        {
            if (request?.Telemetry is null)
            {
                return Results.BadRequest(new { error = "Node telemetry payload is required." });
            }

            var updated = autopilot.RegisterOrUpdateNode(request.Telemetry);
            return Results.Ok(updated);
        })
        .WithName("RegisterEdgeNodeTelemetry")
        .WithSummary("Ingests edge node telemetry and evaluates autonomous health state transitions.");

        // Helper 7. POST /ebpf/metrics/ingest
        group.MapPost("/ebpf/metrics/ingest", (
            [FromBody] IngestEbpfSocketMetricRequest request,
            [FromServices] IEbpfKernelSocketObservabilityEngine engine) =>
        {
            if (request?.Metric is null)
            {
                return Results.BadRequest(new { error = "eBPF socket metric payload is required." });
            }

            engine.IngestMetric(request.Metric);
            return Results.Accepted(value: new { message = "eBPF socket metric ingested successfully." });
        })
        .WithName("IngestEbpfSocketMetric")
        .WithSummary("Ingests a raw eBPF socket metric from a kernel probe.");

        return app;
    }
}
