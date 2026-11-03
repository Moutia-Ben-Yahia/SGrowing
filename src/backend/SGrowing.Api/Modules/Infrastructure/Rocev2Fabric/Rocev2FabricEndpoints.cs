using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Infrastructure.Rocev2Fabric;

public static class Rocev2FabricEndpoints
{
    public static IServiceCollection AddRocev2FabricServices(this IServiceCollection services)
    {
        services.AddSingleton<IRocev2FabricEngine, Rocev2FabricEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRocev2FabricEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/rocev2")
            .WithTags("RoCEv2 RDMA Converged Fabric Arbiter");

        group.MapPost("/nodes", (
            [FromBody] RegisterRoceNodeRequest request,
            [FromServices] IRocev2FabricEngine engine) =>
        {
            var node = engine.RegisterNode(request);
            return Results.Created($"/api/v1/infrastructure/rocev2/nodes/{node.NodeId}", node);
        })
        .WithName("RegisterRoceNode")
        .WithSummary("Enrolls a high-performance GPU node into the lossless RoCEv2 fabric");

        group.MapGet("/nodes", (
            [FromServices] IRocev2FabricEngine engine) =>
        {
            var nodes = engine.GetNodes();
            return Results.Ok(nodes);
        })
        .WithName("GetRoceNodes")
        .WithSummary("Lists all connected RoCEv2 GPU nodes and NIC profiles");

        group.MapPost("/telemetry", (
            [FromQuery] string nodeId,
            [FromQuery] double throughputGbps,
            [FromQuery] double bufferOccupancy,
            [FromQuery] int pfcTx,
            [FromQuery] int pfcRx,
            [FromServices] IRocev2FabricEngine engine) =>
        {
            var sample = engine.IngestTelemetry(nodeId, throughputGbps, bufferOccupancy, pfcTx, pfcRx);
            return Results.Ok(sample);
        })
        .WithName("IngestRoceTelemetry")
        .WithSummary("Ingests real-time buffer occupancy, PFC frames, and ECN marking telemetry");

        group.MapGet("/deadlocks", (
            [FromServices] IRocev2FabricEngine engine) =>
        {
            var alerts = engine.GetActiveDeadlockAlerts();
            return Results.Ok(alerts);
        })
        .WithName("GetRoceDeadlockAlerts")
        .WithSummary("Retrieves all PFC pause frame deadlock events and mitigation statuses");

        group.MapPost("/watchdog-drain/{nodeId}", (
            string nodeId,
            [FromServices] IRocev2FabricEngine engine) =>
        {
            var alert = engine.TriggerWatchdogQueueDrain(nodeId);
            return Results.Ok(alert);
        })
        .WithName("TriggerRoceWatchdogDrain")
        .WithSummary("Triggers PFC watchdog queue drain to break credit loops and recover stalled queues");

        group.MapGet("/dcqcn-tuning", (
            [FromServices] IRocev2FabricEngine engine) =>
        {
            var profile = engine.GetDcqcnTuningProfile();
            return Results.Ok(profile);
        })
        .WithName("GetRoceDcqcnTuning")
        .WithSummary("Provides optimal DCQCN rate control parameters for collective communications");

        group.MapGet("/summary", (
            [FromServices] IRocev2FabricEngine engine) =>
        {
            var summary = engine.GetFabricSummary();
            return Results.Ok(summary);
        })
        .WithName("GetRoceFabricSummary")
        .WithSummary("Returns aggregate fabric throughput, latency, and congestion health metrics");

        return app;
    }
}
