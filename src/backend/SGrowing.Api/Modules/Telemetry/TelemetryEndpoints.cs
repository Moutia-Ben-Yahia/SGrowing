using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Telemetry;

public record IngestEventDto(
    Guid TenantId,
    string SessionId,
    string VisitorId,
    string EventName,
    string Url,
    string? Referrer = "",
    string? DeviceType = "Desktop",
    string? Browser = "Chrome",
    string? CountryCode = "TN",
    string? City = "Tunis",
    string? PropertiesJson = "{}"
);

public record FunnelDropoffRequest(
    Guid TenantId,
    List<string> OrderedStepEvents,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null
);

public static class TelemetryEndpoints
{
    public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/telemetry").WithTags("Product Telemetry & Live Visitor Analytics");

        group.MapPost("/events", async ([FromBody] IngestEventDto req, IProductTelemetryService service) =>
        {
            var ev = await service.IngestTelemetryEventAsync(
                req.TenantId,
                req.SessionId,
                req.VisitorId,
                req.EventName,
                req.Url,
                req.Referrer ?? "",
                req.DeviceType ?? "Desktop",
                req.Browser ?? "Chrome",
                req.CountryCode ?? "TN",
                req.City ?? "Tunis",
                req.PropertiesJson ?? "{}"
            );
            return Results.Ok(ev);
        });

        group.MapGet("/live", async ([FromQuery] Guid tenantId, [FromQuery] int? windowMinutes, IProductTelemetryService service) =>
        {
            var summary = await service.GetLiveSummaryAsync(tenantId, windowMinutes ?? 15);
            return Results.Ok(summary);
        });

        group.MapGet("/events/recent", async ([FromQuery] Guid tenantId, [FromQuery] int? limit, IProductTelemetryService service) =>
        {
            var events = await service.GetRecentEventsAsync(tenantId, limit ?? 50);
            return Results.Ok(events);
        });

        group.MapPost("/funnels/dropoff", async ([FromBody] FunnelDropoffRequest req, IProductTelemetryService service) =>
        {
            var from = req.FromUtc ?? DateTime.UtcNow.AddDays(-30);
            var to = req.ToUtc ?? DateTime.UtcNow;
            var dropoff = await service.ComputeFunnelDropoffAsync(req.TenantId, req.OrderedStepEvents, from, to);
            return Results.Ok(dropoff);
        });

        // Clickstream Buffer & Flush
        group.MapPost("/buffer", async ([FromBody] IngestEventDto req, IClickstreamBufferService bufferService) =>
        {
            var ev = Core.Entities.TelemetryEvent.Create(
                req.TenantId, req.SessionId, req.VisitorId, req.EventName, req.Url,
                req.Referrer ?? "", req.DeviceType ?? "Desktop", req.Browser ?? "Chrome",
                req.CountryCode ?? "TN", req.City ?? "Tunis", req.PropertiesJson ?? "{}");

            await bufferService.BufferEventAsync(ev);
            return Results.Accepted(value: new { buffered = true, pending = bufferService.GetPendingBufferCount() });
        });

        group.MapPost("/buffer/flush", async ([FromQuery] int? batchSize, IClickstreamBufferService bufferService) =>
        {
            var flushed = await bufferService.FlushBufferBatchAsync(batchSize ?? 100);
            return Results.Ok(new { flushedCount = flushed, pendingRemaining = bufferService.GetPendingBufferCount() });
        });

        group.MapGet("/partitions", ([FromQuery] string? table, IDatabasePartitioningService partService) =>
        {
            var baseTable = table ?? "TelemetryEvents";
            var partitions = partService.GetPreconfiguredPartitions(baseTable, DateTime.UtcNow, 4);
            return Results.Ok(partitions);
        });

        return endpoints;
    }
}
