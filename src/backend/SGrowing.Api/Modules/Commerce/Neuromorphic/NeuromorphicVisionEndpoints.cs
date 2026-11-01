using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Horizon 52:
/// Neuromorphic Event-Based Edge Vision &amp; Micro-Payload Conveyor Anomaly Interceptor.
/// </summary>
public static class NeuromorphicVisionEndpoints
{
    /// <summary>
    /// Registers neuromorphic event processing, asynchronous time-surface filtering,
    /// and spiking neural network anomaly detection services in DI.
    /// </summary>
    public static IServiceCollection AddNeuromorphicVisionServices(this IServiceCollection services)
    {
        services.AddSingleton<INeuromorphicEventStreamProcessor, NeuromorphicEventStreamProcessor>();
        services.AddSingleton<IAsynchronousTimeSurfaceFilter, AsynchronousTimeSurfaceFilter>();
        services.AddSingleton<ISpikingNeuralNetworkAnomalyDetector, SpikingNeuralNetworkAnomalyDetector>();

        return services;
    }

    /// <summary>
    /// Maps Neuromorphic Vision and Conveyor Anomaly Interceptor minimal API routes
    /// under /api/v1/commerce/neuromorphic.
    /// </summary>
    public static IEndpointRouteBuilder MapNeuromorphicVisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/neuromorphic")
            .WithTags("Neuromorphic Event-Based Edge Vision & Micro-Payload Conveyor Anomaly Interceptor");

        // 1. POST /api/v1/commerce/neuromorphic/events/ingest
        group.MapPost("/events/ingest", (
            [FromBody] DvsEventIngestRequest req,
            [FromServices] INeuromorphicEventStreamProcessor processor) =>
        {
            if (req.Events == null || req.Events.Count == 0)
            {
                return Results.BadRequest(new DvsEventIngestResponse(
                    Success: false,
                    IngestedCount: 0,
                    FilteredCount: 0,
                    EventRateEventsPerMs: 0.0,
                    PolarBalanceRatio: 1.0,
                    Message: "Events list cannot be null or empty."));
            }

            string streamId = string.IsNullOrWhiteSpace(req.StreamId) ? "stream-default" : req.StreamId;
            string conveyorId = string.IsNullOrWhiteSpace(req.ConveyorId) ? "conveyor-default" : req.ConveyorId;

            var response = processor.IngestEvents(streamId, conveyorId, req.Events);
            return Results.Ok(response);
        })
        .WithName("IngestDvsEvents")
        .WithSummary("Ingests high-throughput microsecond DVS temporal event streams and applies spatio-temporal noise filtering.");

        // 2. POST /api/v1/commerce/neuromorphic/conveyor/analyze
        group.MapPost("/conveyor/analyze", (
            [FromBody] ConveyorAnalysisRequest req,
            [FromServices] INeuromorphicEventStreamProcessor streamProcessor,
            [FromServices] IAsynchronousTimeSurfaceFilter timeSurface,
            [FromServices] ISpikingNeuralNetworkAnomalyDetector anomalyDetector) =>
        {
            if (string.IsNullOrWhiteSpace(req.ConveyorId))
            {
                return Results.BadRequest(new { Success = false, Message = "ConveyorId is required." });
            }

            if (req.Events == null || req.Events.Count == 0)
            {
                return Results.BadRequest(new { Success = false, Message = "Events collection cannot be null or empty." });
            }

            // 1. Filter spatio-temporal noise
            var (validEvents, _) = streamProcessor.FilterNoise(req.Events);

            if (validEvents.Count == 0)
            {
                var emptyResult = new NeuromorphicInspectionResult(
                    AnomalyDetected: false,
                    AnomalyType: ConveyorAnomalyType.None,
                    Severity: AnomalySeverity.Normal,
                    Confidence: 1.0,
                    Description: "All events were classified as background thermal noise or dead pixels.",
                    DetectionTimestampMicroseconds: req.Telemetry?.TimestampMicroseconds ?? 0,
                    SustainedJamDurationMs: 0.0,
                    EmergencyStopTriggered: false,
                    HotspotEvents: [],
                    EstimatedOpticalSpeedMetersPerSec: 0.0);

                return Results.Ok(new ConveyorAnalysisResponse(true, emptyResult, emptyResult.Description));
            }

            // 2. Update continuous exponential decay time surface
            timeSurface.UpdateSurface(validEvents);

            // 3. Compute neuromorphic optical flow velocity
            long currentTs = validEvents[^1].TimestampMicroseconds;
            var (_, _, speedMetersPerSec) = timeSurface.EstimateGlobalVelocity(validEvents, currentTs);

            // 4. Run SNN Leaky Integrate-and-Fire anomaly analysis
            var result = anomalyDetector.AnalyzeConveyor(
                req.ConveyorId,
                validEvents,
                speedMetersPerSec,
                req.Telemetry);

            return Results.Ok(new ConveyorAnalysisResponse(
                Success: true,
                Result: result,
                Message: result.Description));
        })
        .WithName("AnalyzeConveyorEvents")
        .WithSummary("Performs edge LIF spiking neural network analysis and optical flow estimation to detect conveyor anomalies.");

        // 3. GET /api/v1/commerce/neuromorphic/alarms/active
        group.MapGet("/alarms/active", (
            [FromServices] ISpikingNeuralNetworkAnomalyDetector anomalyDetector) =>
        {
            var activeAlarms = anomalyDetector.GetActiveAlarms();
            return Results.Ok(new ActiveAlarmsResponse(activeAlarms));
        })
        .WithName("GetActiveNeuromorphicAlarms")
        .WithSummary("Retrieves all active conveyor anomaly and emergency stop alarms.");

        // 4. GET /api/v1/commerce/neuromorphic/metrics
        group.MapGet("/metrics", (
            [FromQuery] string? streamId,
            [FromServices] INeuromorphicEventStreamProcessor streamProcessor) =>
        {
            var metrics = streamProcessor.GetMetrics(streamId ?? "default");
            return Results.Ok(metrics);
        })
        .WithName("GetNeuromorphicMetrics")
        .WithSummary("Retrieves running aggregate event rates, polar balance ratios, and system health metrics.");

        return app;
    }
}
