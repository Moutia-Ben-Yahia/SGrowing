using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Voice.Streaming;

public static class VoiceAiStreamingEndpoints
{
    public static IEndpointRouteBuilder MapVoiceAiStreamingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/streaming")
            .WithTags("VoiceAiStreamingBridge");

        // POST /session/initiate
        group.MapPost("/session/initiate", async (
            [FromBody] InitiateSessionRequest request,
            IVoiceAiStreamingBridgeService service) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Valid TenantId is required." });
            }

            var session = await service.InitiateSessionAsync(request);
            return Results.Ok(session.ToMetrics());
        })
        .WithName("InitiateVoiceAiStreamingSession")
        .WithSummary("Initiates a full-duplex PCM audio streaming session for inbound IVR or outbound AI voice agents.");

        // POST /session/{id:guid}/audio-frame
        group.MapPost("/session/{id:guid}/audio-frame", async (
            Guid id,
            [FromBody] IngestAudioFrameRequest request,
            IVoiceAiStreamingBridgeService service) =>
        {
            try
            {
                var result = await service.IngestAudioFrameAsync(id, request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Streaming session '{id}' not found." });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("IngestVoiceAudioFrame")
        .WithSummary("Ingests a 20ms linear PCM audio chunk (16kHz 16-bit mono), performs real-time neural VAD and detects barge-in interruptions.");

        // POST /session/{id:guid}/barge-in
        group.MapPost("/session/{id:guid}/barge-in", async (
            Guid id,
            [FromBody] BargeInRequest request,
            IVoiceAiStreamingBridgeService service) =>
        {
            bool success = await service.TriggerBargeInAsync(id, request);
            if (!success)
            {
                return Results.NotFound(new { error = $"Streaming session '{id}' not found or barge-in could not be applied." });
            }

            var metrics = await service.GetSessionMetricsAsync(id);
            return Results.Ok(new
            {
                success = true,
                bargeInCount = metrics?.TotalBargeInCount ?? 1,
                bufferFlushSignal = "FLUSH_OUTBOUND_QUEUE_CLEAR_TTS",
                metrics
            });
        })
        .WithName("TriggerVoiceBargeIn")
        .WithSummary("Explicitly forces an instantaneous barge-in interruption, clearing outbound audio buffers and stopping ongoing TTS.");

        // GET /session/{id:guid}/metrics
        group.MapGet("/session/{id:guid}/metrics", async (
            Guid id,
            IVoiceAiStreamingBridgeService service) =>
        {
            var metrics = await service.GetSessionMetricsAsync(id);
            return metrics is not null
                ? Results.Ok(metrics)
                : Results.NotFound(new { error = $"Streaming session '{id}' not found." });
        })
        .WithName("GetVoiceAiStreamingMetrics")
        .WithSummary("Returns real-time audio oscilloscope levels, turnaround latency distributions, and live transcript stream.");

        // POST /session/{id:guid}/transfer
        group.MapPost("/session/{id:guid}/transfer", async (
            Guid id,
            [FromBody] TransferCallRequest request,
            IVoiceAiStreamingBridgeService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.TargetDesk))
            {
                return Results.BadRequest(new { error = "TargetDesk must be specified for warm transfer." });
            }

            bool success = await service.TransferCallAsync(id, request);
            if (!success)
            {
                return Results.NotFound(new { error = $"Streaming session '{id}' not found." });
            }

            var metrics = await service.GetSessionMetricsAsync(id);
            return Results.Ok(new
            {
                success = true,
                targetDesk = request.TargetDesk,
                reason = request.Reason,
                metrics
            });
        })
        .WithName("TransferVoiceCallToHuman")
        .WithSummary("Performs a zero-data-loss warm SIP/WebRTC call transfer to a human specialist escalation desk.");

        // POST /session/{id:guid}/hangup
        group.MapPost("/session/{id:guid}/hangup", async (
            Guid id,
            [FromBody] HangupCallRequest request,
            IVoiceAiStreamingBridgeService service) =>
        {
            bool success = await service.HangupCallAsync(id, request);
            if (!success)
            {
                return Results.NotFound(new { error = $"Streaming session '{id}' not found." });
            }

            var metrics = await service.GetSessionMetricsAsync(id);
            return Results.Ok(new
            {
                success = true,
                reason = request.Reason,
                metrics
            });
        })
        .WithName("HangupVoiceStreamingCall")
        .WithSummary("Completes the audio session, freezes the live transcript, and finalizes extracted deal insights.");

        // GET /sessions
        group.MapGet("/sessions", async (
            [FromQuery] Guid? tenantId,
            IVoiceAiStreamingBridgeService service) =>
        {
            var sessions = await service.ListSessionsAsync(tenantId);
            return Results.Ok(sessions);
        })
        .WithName("ListVoiceAiStreamingSessions")
        .WithSummary("Lists all active, transferred, and completed real-time voice streaming sessions.");

        // GET /profiles
        group.MapGet("/profiles", async (
            IVoiceAiStreamingBridgeService service) =>
        {
            var profiles = await service.ListAgentProfilesAsync();
            return Results.Ok(profiles);
        })
        .WithName("ListVoiceAiAgentProfiles")
        .WithSummary("Lists all neural TTS agent voice profiles and latency baselines.");

        return app;
    }
}
