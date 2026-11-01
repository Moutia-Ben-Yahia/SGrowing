using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Horizon 51:
/// Autonomous Satellite Direct-to-Cell / Remote Asset Comms &amp; LoRaWAN Mesh Fallback.
/// </summary>
public static class SatelliteCommsEndpoints
{
    /// <summary>
    /// Registers all Horizon 51 Satellite Telemetry, Compressive Burst Buffer, and LoRaWAN Mesh fallback services.
    /// </summary>
    public static IServiceCollection AddSatelliteCommsServices(this IServiceCollection services)
    {
        services.AddSingleton<ISatelliteOrbitalPassScheduler, SatelliteOrbitalPassScheduler>();
        services.AddSingleton<ICompressiveTelemetryBurstBuffer, CompressiveTelemetryBurstBuffer>();
        services.AddSingleton<ILoRaWanAdHocMeshFallbackService, LoRaWanAdHocMeshFallbackService>();

        return services;
    }

    /// <summary>
    /// Maps the Horizon 51 Satellite &amp; LoRaWAN Comms Minimal API endpoints under /api/v1/logistics/satellite.
    /// </summary>
    public static IEndpointRouteBuilder MapSatelliteCommsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/satellite")
            .WithTags("Satellite Direct-to-Cell & IoT Mesh Fallback");

        // 1. POST /api/v1/logistics/satellite/telemetry/queue
        group.MapPost("/telemetry/queue", (
            [FromBody] QueueTelemetryRequest request,
            [FromServices] ICompressiveTelemetryBurstBuffer buffer,
            [FromServices] ISatelliteOrbitalPassScheduler scheduler) =>
        {
            if (request?.Reading is null)
            {
                return Results.BadRequest(new { Error = "Reading payload cannot be null." });
            }

            var reading = request.Reading;
            if (string.IsNullOrWhiteSpace(reading.ReadingId) || string.IsNullOrWhiteSpace(reading.AssetId))
            {
                return Results.BadRequest(new { Error = "ReadingId and AssetId are required." });
            }

            if (reading.Latitude < -90.0 || reading.Latitude > 90.0 || reading.Longitude < -180.0 || reading.Longitude > 180.0)
            {
                return Results.BadRequest(new { Error = "Coordinates out of bounds (Latitude: [-90, 90], Longitude: [-180, 180])." });
            }

            // Check opportunistic immediate burst transmission if satellite is in LOS right now
            if (request.ImmediateBurstIfVisible &&
                scheduler.CheckLineOfSight(reading.Latitude, reading.Longitude, DateTime.UtcNow, out var visiblePass) &&
                visiblePass is not null)
            {
                var frame = buffer.CompressReading(reading);
                return Results.Ok(new QueueTelemetryResponse(
                    Queued: false,
                    BurstTransmitted: true,
                    FrameId: frame.FrameId,
                    Priority: frame.Priority,
                    MediumUsed: TransmissionMedium.SatelliteUplink,
                    BufferQueueDepth: buffer.GetStatus().TotalQueued,
                    Message: $"Immediate burst uplink transmitted via active {visiblePass.Constellation} LOS pass window ({visiblePass.CurrentElevationDegrees:F1}° elevation)."
                ));
            }

            // Otherwise, buffer with head-of-line priority preemption
            var queuedFrame = buffer.Enqueue(reading);
            return Results.Accepted($"/api/v1/logistics/satellite/buffer/status", new QueueTelemetryResponse(
                Queued: true,
                BurstTransmitted: false,
                FrameId: queuedFrame.FrameId,
                Priority: queuedFrame.Priority,
                MediumUsed: TransmissionMedium.SatelliteUplink,
                BufferQueueDepth: buffer.GetStatus().TotalQueued,
                Message: "Telemetry frame compressed and queued in priority ring buffer awaiting orbital pass window."
            ));
        })
        .WithName("QueueSatelliteTelemetry")
        .WithSummary("Queues or immediately bursts compressed telemetry frame via satellite LEO link.");

        // 2. GET /api/v1/logistics/satellite/passes/{latitude}/{longitude}
        group.MapGet("/passes/{latitude:double}/{longitude:double}", (
            [FromRoute] double latitude,
            [FromRoute] double longitude,
            [FromQuery] int? lookaheadHours,
            [FromQuery] ConstellationType? constellation,
            [FromServices] ISatelliteOrbitalPassScheduler scheduler) =>
        {
            if (latitude < -90.0 || latitude > 90.0 || longitude < -180.0 || longitude > 180.0)
            {
                return Results.BadRequest(new { Error = "Coordinates out of bounds (Latitude: [-90, 90], Longitude: [-180, 180])." });
            }

            var horizon = TimeSpan.FromHours(lookaheadHours is > 0 and <= 48 ? lookaheadHours.Value : 6);
            var now = DateTime.UtcNow;
            var hasLos = scheduler.CheckLineOfSight(latitude, longitude, now, out var visiblePass, constellation);
            var passes = scheduler.GetUpcomingPasses(latitude, longitude, now, horizon, constellation).ToList();

            if (visiblePass is not null && !passes.Any(p => p.SatelliteId == visiblePass.SatelliteId && p.StartUtc == visiblePass.StartUtc))
            {
                passes.Insert(0, visiblePass);
            }

            var recommended = constellation?.ToString() ??
                (passes.MaxBy(p => p.MaxElevationDegrees)?.Constellation.ToString() ?? nameof(ConstellationType.StarlinkDirectToCell));

            return Results.Ok(new OrbitalPassResponse(
                Latitude: latitude,
                Longitude: longitude,
                EvaluatedAtUtc: now,
                HasLineOfSight: hasLos,
                UpcomingPasses: passes,
                RecommendedConstellation: recommended
            ));
        })
        .WithName("GetSatellitePasses")
        .WithSummary("Calculates current LOS visibility and upcoming satellite orbital passes for given coordinates.");

        // 3. POST /api/v1/logistics/satellite/lora/relay
        group.MapPost("/lora/relay", (
            [FromBody] LoRaRelayRequest request,
            [FromServices] ILoRaWanAdHocMeshFallbackService meshService) =>
        {
            if (request?.Packet is null)
            {
                return Results.BadRequest(new { Error = "Packet payload cannot be null." });
            }

            if (string.IsNullOrWhiteSpace(request.ReportingNodeId))
            {
                return Results.BadRequest(new { Error = "ReportingNodeId is required." });
            }

            var response = meshService.ProcessRelay(request);
            return Results.Ok(response);
        })
        .WithName("RelayLoRaMeshPacket")
        .WithSummary("Processes an ad-hoc multi-hop LoRa mesh packet with deduplication and sink detection.");

        // 4. GET /api/v1/logistics/satellite/buffer/status
        group.MapGet("/buffer/status", (
            [FromServices] ICompressiveTelemetryBurstBuffer buffer) =>
        {
            var status = buffer.GetStatus();
            return Results.Ok(new BufferStatusResponse(status));
        })
        .WithName("GetSatelliteBufferStatus")
        .WithSummary("Retrieves current satellite burst buffer depth, priority counts, and compression metrics.");

        return app;
    }
}
