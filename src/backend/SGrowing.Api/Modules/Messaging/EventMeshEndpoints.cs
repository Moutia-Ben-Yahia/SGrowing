using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Messaging;

public record PublishEventApiRequest(
    Guid? TenantId,
    string Topic,
    string? PartitionKey,
    string? EventId,
    string? EventType,
    string Payload,
    EventBrokerMode? BrokerMode = null,
    Dictionary<string, string>? Headers = null
);

public record BatchPublishApiRequest(
    List<PublishEventApiRequest> Events,
    EventBrokerMode? BrokerMode = null
);

public static class EventMeshEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapEventMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/messaging/event-mesh")
            .WithTags("Distributed Event Mesh & Multi-Broker Streaming Bridge");

        // 1. POST /publish
        group.MapPost("/publish", async (
            [FromBody] PublishEventApiRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IDistributedEventMeshService eventMeshService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(req.TenantId, headerTenantId);
            var publishReq = new PublishEventRequest(
                TenantId: effectiveTenantId,
                Topic: req.Topic,
                PartitionKey: req.PartitionKey,
                EventId: req.EventId,
                EventType: req.EventType,
                Payload: req.Payload,
                BrokerMode: req.BrokerMode,
                Headers: req.Headers
            );

            var result = await eventMeshService.PublishAsync(publishReq, ct);
            return Results.Ok(result);
        })
        .WithName("PublishMeshEvent")
        .WithSummary("Publish an event to the distributed event mesh with deduplication and partition routing");

        // 2. POST /publish-batch
        group.MapPost("/publish-batch", async (
            [FromBody] BatchPublishApiRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IDistributedEventMeshService eventMeshService,
            CancellationToken ct) =>
        {
            var requests = (req.Events ?? new()).Select(e => new PublishEventRequest(
                TenantId: ResolveTenantId(e.TenantId, headerTenantId),
                Topic: e.Topic,
                PartitionKey: e.PartitionKey,
                EventId: e.EventId,
                EventType: e.EventType,
                Payload: e.Payload,
                BrokerMode: e.BrokerMode ?? req.BrokerMode,
                Headers: e.Headers
            ));

            var result = await eventMeshService.PublishBatchAsync(requests, ct);
            return Results.Ok(result);
        })
        .WithName("PublishMeshEventBatch")
        .WithSummary("Publish a batch of events to the distributed event mesh concurrently");

        // 3. GET /metrics
        group.MapGet("/metrics", (
            IDistributedEventMeshService eventMeshService) =>
        {
            var metrics = eventMeshService.GetMetrics();
            return Results.Ok(metrics);
        })
        .WithName("GetEventMeshMetrics")
        .WithSummary("Get real-time telemetry metrics: published count, consumer lag, deduplication drops, DLQ size");

        // 4. GET /partitions
        group.MapGet("/partitions", (
            IDistributedEventMeshService eventMeshService) =>
        {
            var partitions = eventMeshService.GetPartitions();
            return Results.Ok(partitions);
        })
        .WithName("GetEventMeshPartitions")
        .WithSummary("Get partition balancer distribution, consumer lag, and assigned workers");

        // 5. GET /dlq
        group.MapGet("/dlq", (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? topic,
            [FromQuery] DlqStatus? status,
            [FromQuery] int? limit,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IDistributedEventMeshService eventMeshService) =>
        {
            var effectiveTenant = tenantId ?? headerTenantId;
            var items = eventMeshService.GetDlqItems(effectiveTenant, topic, status, limit ?? 100);
            return Results.Ok(items);
        })
        .WithName("GetEventMeshDlq")
        .WithSummary("Retrieve Dead-Letter Queue items with failure reasons and attempt counts");

        // 6. POST /dlq/replay
        group.MapPost("/dlq/replay", async (
            [FromBody] ReplayDlqRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IDistributedEventMeshService eventMeshService,
            CancellationToken ct) =>
        {
            var effectiveTenant = req.TenantId ?? headerTenantId;
            var result = await eventMeshService.ReplayDlqAsync(req with { TenantId = effectiveTenant }, ct);
            return Results.Ok(result);
        })
        .WithName("ReplayEventMeshDlq")
        .WithSummary("Replay single or bulk Dead-Letter Queue events into active event mesh");

        // 7. POST /dlq/purge
        group.MapPost("/dlq/purge", async (
            [FromBody] PurgeDlqRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            IDistributedEventMeshService eventMeshService,
            CancellationToken ct) =>
        {
            var effectiveTenant = req.TenantId ?? headerTenantId;
            var result = await eventMeshService.PurgeDlqAsync(req with { TenantId = effectiveTenant }, ct);
            return Results.Ok(result);
        })
        .WithName("PurgeEventMeshDlq")
        .WithSummary("Purge events from the Dead-Letter Queue");

        return app;
    }
}
