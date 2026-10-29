using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Voice;

public record ClassifyAmdRequest(
    string DestinationPhone,
    int? AudioDurationMs = 2100,
    string? ForcedClassification = null,
    Guid? CallSessionId = null
);

public record ConfigureVoicemailTemplateRequest(
    string TemplateName,
    string AudioUrl,
    string? TextPrompt = "",
    bool? UseTts = false
);

public record VoiceStreamTurnRequest(
    Guid CallSessionId,
    string? AudioPayloadBase64 = null,
    string? AudioEncoding = "mulaw",
    string? TranscriptText = null,
    string? StreamSid = "MZ_DEFAULT",
    bool ForceAgentSpeaking = false
);

public record VoiceTranscriptionChunkRequest(
    Guid CallSessionId,
    string Speaker,
    string ChunkText,
    double? SentimentScore = null,
    bool IsFinal = false
);

public record AnalyzeVoiceSentimentRequest(
    string SpeechText
);

public static class VoiceCallAmdEndpoints
{
    public static IEndpointRouteBuilder MapVoiceCallAmdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/voice/amd")
            .WithTags("VoiceAmd");

        group.MapPost("/classify", async (
            [FromQuery] Guid tenantId,
            [FromBody] ClassifyAmdRequest req,
            IVoiceCallAmdService service) =>
        {
            var result = await service.ClassifyAudioStreamAmdAsync(
                tenantId,
                req.DestinationPhone,
                req.AudioDurationMs ?? 2100,
                req.ForcedClassification,
                req.CallSessionId);

            return Results.Ok(result);
        });

        group.MapPost("/templates", async (
            [FromQuery] Guid tenantId,
            [FromBody] ConfigureVoicemailTemplateRequest req,
            IVoiceCallAmdService service) =>
        {
            var template = await service.ConfigureVoicemailTemplateAsync(
                tenantId,
                req.TemplateName,
                req.AudioUrl,
                req.TextPrompt ?? "",
                req.UseTts ?? false);

            return Results.Ok(template);
        });

        group.MapGet("/records", async (
            [FromQuery] Guid tenantId,
            [FromQuery] int? limit,
            IVoiceCallAmdService service) =>
        {
            var records = await service.GetAmdRecordsAsync(tenantId, limit ?? 50);
            return Results.Ok(records);
        });

        // Frontier 1: Real-Time Conversational AI Voice Stream Endpoints
        group.MapPost("/stream-turn", async (
            [FromQuery] Guid tenantId,
            [FromBody] VoiceStreamTurnRequest req,
            VoiceAiStreamService voiceAiService,
            CancellationToken ct) =>
        {
            byte[]? audioBytes = null;
            if (!string.IsNullOrWhiteSpace(req.AudioPayloadBase64))
            {
                try
                {
                    audioBytes = Convert.FromBase64String(req.AudioPayloadBase64);
                }
                catch { }
            }

            var tracker = voiceAiService.GetOrCreateTracker(req.CallSessionId, req.StreamSid ?? "MZ_DEFAULT");
            if (req.ForceAgentSpeaking)
            {
                tracker.IsAgentSpeaking = true;
            }

            var result = await voiceAiService.ExecuteConversationalTurnAsync(
                tenantId,
                req.CallSessionId,
                req.StreamSid ?? "MZ_DEFAULT",
                audioBytes,
                req.AudioEncoding ?? "mulaw",
                req.TranscriptText,
                tracker,
                ct);

            return Results.Ok(result);
        });

        group.MapPost("/transcription-chunk", async (
            [FromQuery] Guid tenantId,
            [FromBody] VoiceTranscriptionChunkRequest req,
            VoiceAiStreamService voiceAiService,
            CancellationToken ct) =>
        {
            var sentiment = req.SentimentScore ?? voiceAiService.CalculateSentimentScore(req.ChunkText);
            var signals = voiceAiService.ExtractBuyingSignals(req.ChunkText);

            var tracker = voiceAiService.GetOrCreateTracker(req.CallSessionId);
            tracker.CurrentSentimentScore = sentiment;
            foreach (var sig in signals)
            {
                if (!tracker.DetectedBuyingSignals.Contains(sig))
                {
                    tracker.DetectedBuyingSignals.Add(sig);
                }
            }

            await voiceAiService.BroadcastTranscriptionChunkAsync(
                tenantId,
                req.CallSessionId,
                req.Speaker,
                req.ChunkText,
                sentiment,
                req.IsFinal,
                ct);

            return Results.Ok(new
            {
                req.CallSessionId,
                req.Speaker,
                req.ChunkText,
                SentimentScore = sentiment,
                req.IsFinal,
                BuyingSignals = signals
            });
        });

        group.MapPost("/sentiment-analyze", (
            [FromBody] AnalyzeVoiceSentimentRequest req,
            VoiceAiStreamService voiceAiService) =>
        {
            var sentiment = voiceAiService.CalculateSentimentScore(req.SpeechText);
            var signals = voiceAiService.ExtractBuyingSignals(req.SpeechText);

            return Results.Ok(new
            {
                req.SpeechText,
                SentimentScore = sentiment,
                BuyingSignals = signals,
                IsHighPurchaseIntent = signals.Any(s => s is "PricingInquiry" or "ContractRequest" or "BudgetApproved"),
                IsCompetitorRisk = signals.Contains("CompetitorObjection")
            });
        });

        group.MapGet("/session/{sessionId:guid}/telemetry", (
            Guid sessionId,
            VoiceAiStreamService voiceAiService) =>
        {
            var tracker = voiceAiService.GetOrCreateTracker(sessionId);
            return Results.Ok(new
            {
                tracker.SessionId,
                tracker.StreamSid,
                VadStatus = tracker.CurrentVadStatus.ToString(),
                tracker.IsAgentSpeaking,
                tracker.InterruptionCount,
                tracker.TotalFramesReceived,
                tracker.LastRmsEnergy,
                tracker.AverageProcessingLatencyMs,
                tracker.CurrentSentimentScore,
                tracker.DetectedBuyingSignals,
                tracker.TranscriptHistory,
                tracker.LastActivityUtc
            });
        });

        return app;
    }
}
