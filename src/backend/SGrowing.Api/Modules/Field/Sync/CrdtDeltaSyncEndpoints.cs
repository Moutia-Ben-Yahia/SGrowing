using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Field.Sync;

public static class CrdtDeltaSyncEndpoints
{
    /// <summary>
    /// Registers offline-first CRDT delta sync replication services into the DI container.
    /// </summary>
    public static IServiceCollection AddCrdtDeltaSyncServices(this IServiceCollection services)
    {
        services.AddSingleton<ICrdtStateReplicatorService, CrdtStateReplicatorService>();
        return services;
    }

    /// <summary>
    /// Maps REST endpoints for the offline-first field companion delta-sync CRDT engine.
    /// </summary>
    public static IEndpointRouteBuilder MapCrdtDeltaSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/field/sync")
            .WithTags("Offline-First Field Companion & Delta-Sync CRDT Engine");

        // 1. Bidirectional push/pull sync
        group.MapPost("/delta", async (
            [FromBody] SyncBatchRequest req,
            ICrdtStateReplicatorService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.DeviceId))
            {
                return Results.BadRequest(new { error = "DeviceId cannot be empty." });
            }

            var response = await service.ReplicateAsync(req, ct);
            return Results.Ok(response);
        })
        .WithName("FieldSyncDelta")
        .WithSummary("Performs bidirectional push/pull delta replication with edge field devices.");

        // 2. Initial bootstrap snapshot for a driver
        group.MapGet("/manifests/{driverId}/snapshot", async (
            string driverId,
            ICrdtStateReplicatorService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(driverId))
            {
                return Results.BadRequest(new { error = "DriverId cannot be empty." });
            }

            var manifests = await service.GetDriverManifestsSnapshotAsync(driverId, ct);
            return Results.Ok(manifests);
        })
        .WithName("GetDriverManifestsSnapshot")
        .WithSummary("Returns current manifest snapshots assigned to a driver for initial edge bootstrap.");

        // 3. Submit offline Proof-of-Delivery
        group.MapPost("/manifests/{manifestId:guid}/pod", async (
            Guid manifestId,
            [FromBody] SubmitPodRequest req,
            ICrdtStateReplicatorService service,
            CancellationToken ct) =>
        {
            if (req.PodData is null)
            {
                return Results.BadRequest(new { error = "PodData cannot be null." });
            }

            var updated = await service.SubmitProofOfDeliveryAsync(
                manifestId,
                req.StopId,
                req.PodData,
                req.NodeId ?? "edge-pod",
                ct);

            return Results.Ok(updated);
        })
        .WithName("SubmitProofOfDelivery")
        .WithSummary("Ingests offline proof-of-delivery (signature, geocode, photo hash) for a manifest.");

        // 4. Engine health status check
        group.MapGet("/health", (ICrdtStateReplicatorService service) =>
        {
            return Results.Ok(service.GetHealth());
        })
        .WithName("CrdtSyncHealth")
        .WithSummary("Returns health check status and synchronization metrics for the CRDT engine.");

        return app;
    }
}
