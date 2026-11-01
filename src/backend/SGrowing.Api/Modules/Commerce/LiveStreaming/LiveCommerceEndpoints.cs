using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.LiveStreaming;

/// <summary>
/// Minimal API endpoints and Dependency Injection setup for Phase 20:
/// Interactive Live Commerce &amp; Real-Time Video AI Co-Host Engine.
/// </summary>
public static class LiveCommerceEndpoints
{
    /// <summary>
    /// Registers singleton live streaming and AI Co-Host services into the container.
    /// </summary>
    public static IServiceCollection AddLiveCommerceServices(this IServiceCollection services)
    {
        services.AddSingleton<ILiveCommerceStreamingService, LiveCommerceStreamingService>();
        services.AddSingleton<ILiveVideoAiCoHostService, LiveVideoAiCoHostService>();

        return services;
    }

    /// <summary>
    /// Maps the interactive live commerce route group under /api/v1/commerce/live.
    /// </summary>
    public static IEndpointRouteBuilder MapLiveCommerceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/live")
            .WithTags("Interactive Live Commerce & Real-Time Video AI Co-Host Engine");

        // 1. POST /api/v1/commerce/live/sessions - Create broadcast session
        group.MapPost("/sessions", async (
            [FromBody] CreateLiveSessionRequest req,
            [FromServices] ILiveCommerceStreamingService streamingService,
            CancellationToken ct) =>
        {
            var session = await streamingService.CreateSessionAsync(
                req.HostId,
                req.Title,
                req.Description,
                ct);

            return Results.Created($"/api/v1/commerce/live/sessions/{session.SessionId}", session);
        })
        .WithName("CreateLiveStreamSession")
        .WithSummary("Create a new interactive broadcast session with WebRTC WHIP ingest and WHEP playback endpoints.");

        // 2. POST /api/v1/commerce/live/sessions/{id}/start - Start broadcast
        group.MapPost("/sessions/{id}/start", async (
            string id,
            [FromServices] ILiveCommerceStreamingService streamingService,
            CancellationToken ct) =>
        {
            try
            {
                var session = await streamingService.StartSessionAsync(id, ct);
                return Results.Ok(session);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("StartLiveStreamSession")
        .WithSummary("Activate the live stream broadcast and initiate real-time viewer tracking.");

        // 3. POST /api/v1/commerce/live/sessions/{id}/pin - Pin flash product
        group.MapPost("/sessions/{id}/pin", async (
            string id,
            [FromBody] PinProductRequest req,
            [FromServices] ILiveCommerceStreamingService streamingService,
            CancellationToken ct) =>
        {
            try
            {
                var pinned = await streamingService.PinProductAsync(
                    id,
                    req.ProductId,
                    req.Sku,
                    req.Title,
                    req.OriginalPrice,
                    req.FlashSalePrice,
                    req.StockLimit,
                    TimeSpan.FromSeconds(req.DurationSeconds),
                    ct);

                return Results.Ok(pinned);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("PinFlashProduct")
        .WithSummary("Pin an exclusive flash deal to the stream overlay with a live countdown timer and limited stock.");

        // 4. POST /api/v1/commerce/live/sessions/{id}/unpin - Unpin flash product
        group.MapPost("/sessions/{id}/unpin", async (
            string id,
            [FromServices] ILiveCommerceStreamingService streamingService,
            CancellationToken ct) =>
        {
            try
            {
                var success = await streamingService.UnpinProductAsync(id, ct);
                return Results.Ok(new { unpinned = success });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("UnpinFlashProduct")
        .WithSummary("Remove or deactivate the currently pinned flash deal from the broadcast overlay.");

        // 5. POST /api/v1/commerce/live/sessions/{id}/reserve - Reserve flash deal stock
        group.MapPost("/sessions/{id}/reserve", async (
            string id,
            [FromBody] ReserveFlashStockRequest req,
            [FromServices] ILiveCommerceStreamingService streamingService,
            CancellationToken ct) =>
        {
            try
            {
                var reservation = await streamingService.ReserveFlashStockAsync(
                    id,
                    req.ProductId,
                    req.CustomerId,
                    req.Quantity,
                    ct);

                return reservation.Success
                    ? Results.Ok(reservation)
                    : Results.UnprocessableEntity(reservation);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ReserveFlashDealStock")
        .WithSummary("Lock inventory for a viewer during an active live flash deal countdown.");

        // 6. POST /api/v1/commerce/live/sessions/{id}/chat - Post comment & trigger AI Co-Host processing
        group.MapPost("/sessions/{id}/chat", async (
            string id,
            [FromBody] PostChatMessageRequest req,
            [FromServices] ILiveVideoAiCoHostService aiCoHostService,
            CancellationToken ct) =>
        {
            var result = await aiCoHostService.ProcessIncomingMessageAsync(
                id,
                req.UserId,
                req.Username,
                req.Text,
                ct);

            return Results.Ok(result);
        })
        .WithName("PostLiveChatMessage")
        .WithSummary("Submit a viewer chat message, process real-time moderation, sentiment, intent, auto-reply, and host earpiece cue.");

        // 7. GET /api/v1/commerce/live/sessions/{id}/cues - Get host earpiece cue feed
        group.MapGet("/sessions/{id}/cues", async (
            string id,
            [FromServices] ILiveVideoAiCoHostService aiCoHostService,
            CancellationToken ct) =>
        {
            var cues = await aiCoHostService.GetPendingHostCuesAsync(id, ct);
            return Results.Ok(cues);
        })
        .WithName("GetHostPendingCues")
        .WithSummary("Retrieve prioritized actionable prompts for the live host earpiece monitor.");

        // 8. POST /api/v1/commerce/live/sessions/{id}/end - Conclude broadcast
        group.MapPost("/sessions/{id}/end", async (
            string id,
            [FromServices] ILiveCommerceStreamingService streamingService,
            CancellationToken ct) =>
        {
            try
            {
                var session = await streamingService.EndSessionAsync(id, ct);
                return Results.Ok(session);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("EndLiveStreamSession")
        .WithSummary("Conclude the live broadcast and finalize sales metrics and peak viewership stats.");

        // 9. GET /api/v1/commerce/live/health - Engine status check
        group.MapGet("/health", async (
            [FromServices] ILiveCommerceStreamingService streamingService,
            CancellationToken ct) =>
        {
            var sessions = await streamingService.GetAllSessionsAsync(ct);
            var activeCount = sessions.Count(s => s.Status == LiveStreamStatus.Live);

            var response = new LiveCommerceHealthResponse(
                Status: "Healthy",
                Service: "Interactive Live Commerce & Real-Time Video AI Co-Host Engine",
                ActiveSessionsCount: activeCount,
                TimestampUtc: DateTime.UtcNow);

            return Results.Ok(response);
        })
        .WithName("GetLiveCommerceHealth")
        .WithSummary("Check the operational health of the live commerce streaming and AI Co-Host engine.");

        return app;
    }
}
