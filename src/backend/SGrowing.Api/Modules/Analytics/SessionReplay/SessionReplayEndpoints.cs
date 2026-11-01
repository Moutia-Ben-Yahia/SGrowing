using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Minimal API endpoints and Dependency Injection setup for Phase 32:
/// Real-Time Session Replay, DOM Mutation Vector Journal &amp; UX Heatmap Reconstruction.
/// </summary>
public static class SessionReplayEndpoints
{
    /// <summary>
    /// Registers session replay, PII scrubbing vault, and interaction heatmap services in DI.
    /// </summary>
    public static IServiceCollection AddSessionReplayServices(this IServiceCollection services)
    {
        services.AddSingleton<IPiiScrubbingVaultService, PiiScrubbingVaultService>();
        services.AddSingleton<IDomMutationVectorEngine, DomMutationVectorEngine>();
        services.AddSingleton<IUserInteractionHeatmapService, UserInteractionHeatmapService>();
        services.AddSingleton<ISessionReplayCoordinator, SessionReplayCoordinator>();

        return services;
    }

    /// <summary>
    /// Maps session replay and heatmap reconstruction endpoints under /api/v1/analytics/session-replay.
    /// </summary>
    public static IEndpointRouteBuilder MapSessionReplayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics/session-replay")
            .WithTags("Real-Time Session Replay, DOM Mutation Journal & UX Heatmap Reconstruction");

        // 1. POST /sessions/start - Initialize recording lifecycle
        group.MapPost("/sessions/start", (
            [FromBody] SessionStartRequest req,
            [FromServices] ISessionReplayCoordinator coordinator) =>
        {
            if (string.IsNullOrWhiteSpace(req.SessionId) || string.IsNullOrWhiteSpace(req.TenantId))
            {
                return Results.BadRequest(new { error = "SessionId and TenantId are mandatory." });
            }

            var session = coordinator.StartSession(req);
            return Results.Ok(session);
        })
        .WithName("StartSessionReplay")
        .WithSummary("Initializes recording lifecycle for a new user session.");

        // 2. POST /sessions/{sessionId}/ingest - Ingest DOM mutations and telemetry
        group.MapPost("/sessions/{sessionId}/ingest", (
            string sessionId,
            [FromBody] SessionIngestPayload payload,
            [FromServices] IPiiScrubbingVaultService scrubber,
            [FromServices] IDomMutationVectorEngine mutationEngine,
            [FromServices] IUserInteractionHeatmapService heatmapService) =>
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Results.BadRequest(new { error = "Invalid session identifier." });
            }

            int mutationCount = 0;
            double bandwidthKbps = 0.0;

            if (payload.MutationBatch != null && payload.MutationBatch.Mutations.Count > 0)
            {
                var scrubbedBatch = scrubber.ScrubMutationBatch(payload.MutationBatch);
                var ingestResult = mutationEngine.IngestMutations(sessionId, scrubbedBatch);
                if (!ingestResult.Success)
                {
                    return Results.BadRequest(new { error = ingestResult.Message });
                }
                mutationCount = ingestResult.ProcessedCount;
                bandwidthKbps = ingestResult.BandwidthKbps;
            }

            int interactionCount = 0;
            if (payload.Interactions != null && payload.Interactions.Count > 0)
            {
                interactionCount = heatmapService.IngestInteractions(sessionId, payload.PageUrl ?? string.Empty, payload.Interactions);
            }

            return Results.Ok(new
            {
                sessionId,
                mutationsIngested = mutationCount,
                interactionsIngested = interactionCount,
                bandwidthKbps,
                isWithinFootprint = bandwidthKbps < 2.0
            });
        })
        .WithName("IngestSessionStream")
        .WithSummary("Ingests PII-scrubbed DOM mutation vectors and high-frequency interaction telemetry.");

        // 3. POST /sessions/{sessionId}/complete - Finalize recording
        group.MapPost("/sessions/{sessionId}/complete", (
            string sessionId,
            [FromServices] ISessionReplayCoordinator coordinator) =>
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Results.BadRequest(new { error = "Invalid session identifier." });
            }

            var session = coordinator.CompleteSession(sessionId);
            return Results.Ok(session);
        })
        .WithName("CompleteSessionReplay")
        .WithSummary("Finalizes session recording and calculates aggregated metrics.");

        // 4. GET /sessions/{sessionId}/timeline - Reassemble playback timeline
        group.MapGet("/sessions/{sessionId}/timeline", (
            string sessionId,
            [FromQuery] double? playbackSpeed,
            [FromServices] ISessionReplayCoordinator coordinator) =>
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Results.BadRequest(new { error = "Invalid session identifier." });
            }

            var timeline = coordinator.GetReplayTimeline(sessionId, playbackSpeed ?? 1.0);
            return Results.Ok(timeline);
        })
        .WithName("GetSessionReplayTimeline")
        .WithSummary("Reassembles a unified chronological playback stream with variable speed scaling.");

        // 5. GET /sessions/{sessionId}/frustrations - Retrieve detected micro-frustrations
        group.MapGet("/sessions/{sessionId}/frustrations", (
            string sessionId,
            [FromServices] IUserInteractionHeatmapService heatmapService) =>
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Results.BadRequest(new { error = "Invalid session identifier." });
            }

            var frustrations = heatmapService.DetectFrustrations(sessionId);
            return Results.Ok(frustrations);
        })
        .WithName("GetSessionFrustrations")
        .WithSummary("Identifies micro-frustrations including rage clicks, dead clicks, and scroll thrashing.");

        // 6. GET /heatmaps - 2D spatial density reconstruction
        group.MapGet("/heatmaps", (
            [FromQuery] string pageUrl,
            [FromQuery] HeatmapType? type,
            [FromQuery] int? gridWidth,
            [FromQuery] int? gridHeight,
            [FromServices] IUserInteractionHeatmapService heatmapService) =>
        {
            if (string.IsNullOrWhiteSpace(pageUrl))
            {
                return Results.BadRequest(new { error = "pageUrl query parameter is mandatory." });
            }

            var heatmap = heatmapService.GetHeatmap(
                pageUrl,
                type ?? HeatmapType.Click,
                gridWidth ?? 50,
                gridHeight ?? 50);

            return Results.Ok(heatmap);
        })
        .WithName("GetPageHeatmap")
        .WithSummary("Computes 2D spatial density heatmap for clicks, pointer movement, or scrolling.");

        // 7. GET /sessions/active - List active recording sessions
        group.MapGet("/sessions/active", (
            [FromQuery] string? tenantId,
            [FromServices] ISessionReplayCoordinator coordinator) =>
        {
            var sessions = coordinator.ListActiveSessions(tenantId ?? string.Empty);
            return Results.Ok(sessions);
        })
        .WithName("ListActiveSessions")
        .WithSummary("Lists actively recording sessions filtered by tenant.");

        return app;
    }
}
