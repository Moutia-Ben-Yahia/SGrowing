using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Telemetry;

public static class SegmentTelemetryEndpoints
{
    public static IEndpointRouteBuilder MapSegmentTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/telemetry")
            .WithTags("Segment-Compatible Telemetry & Durable Journeys");

        group.MapPost("/identify", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] SegmentIdentifyDto request,
            ISegmentTelemetryAndJourneysService service) =>
        {
            var result = await service.RecordIdentifyEventAsync(tenantId, request);
            return Results.Ok(new { Success = result });
        });

        group.MapPost("/track", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromBody] SegmentTrackDto request,
            ISegmentTelemetryAndJourneysService service) =>
        {
            var result = await service.RecordTrackEventAsync(tenantId, request);
            return Results.Ok(new { Success = result });
        });

        group.MapPost("/journeys/{id:guid}/evaluate-durable", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromQuery] string userId,
            [FromQuery] string eventName,
            ISegmentTelemetryAndJourneysService service) =>
        {
            var result = await service.EvaluateDurableJourneyStepAsync(tenantId, id, userId, eventName ?? "page_viewed");
            return Results.Ok(result);
        });

        return app;
    }
}
