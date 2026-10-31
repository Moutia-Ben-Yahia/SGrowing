using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Api.Modules.Voice.Streaming;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Voice;

public static class VoiceEndpoints
{
    public static IEndpointRouteBuilder MapVoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/voice/inbound", async (HttpRequest request, [FromQuery] Guid tenantId, [FromQuery] string callSid, [FromQuery] string from, [FromQuery] string to, VoiceTelephonyService voiceService) =>
        {
            var twiml = await voiceService.HandleInboundCallWebhookAsync(tenantId, callSid, from, to);
            return Results.Content(twiml, "application/xml");
        });

        app.MapPost("/api/voice/turn", async ([FromQuery] Guid tenantId, [FromBody] VoiceTurnDto req, RealtimeVoiceAgentService voiceAgent) =>
        {
            var responseSpeech = await voiceAgent.ProcessVoiceTurnAsync(tenantId, req.SessionId, req.CallerSpeechTranscript);
            return Results.Ok(new { response = responseSpeech });
        });

        app.MapGet("/api/voice/sessions", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var sessions = await db.VoiceCallSessions.Where(s => s.TenantId == tenantId).OrderByDescending(s => s.CreatedAtUtc).ToListAsync();
            return Results.Ok(sessions);
        });

        // Real-Time Streaming Voice Pipeline
        app.MapPost("/api/voice/streaming/frame", ([FromBody] StreamAudioChunkDto req, StreamingVoiceWebSocketHandler handler, SGrowingDbContext db) =>
        {
            var session = db.StreamingVoiceCallSessions.FirstOrDefault(s => s.TenantId == req.TenantId && s.Id == req.SessionId)
                          ?? StreamingVoiceCallSession.Create(req.TenantId, req.CallSid, req.StreamSid);

            if (!db.StreamingVoiceCallSessions.Contains(session))
            {
                db.StreamingVoiceCallSessions.Add(session);
            }

            var result = handler.ProcessInboundAudioChunk(session, req.PayloadBase64, req.SequenceNumber);
            db.SaveChanges();

            return Results.Ok(result);
        });

        app.MapPost("/api/voice/streaming/transfer", ([FromBody] WarmTransferDto req, BargeInInterruptionService transferService, SGrowingDbContext db) =>
        {
            var session = db.StreamingVoiceCallSessions.FirstOrDefault(s => s.TenantId == req.TenantId && s.Id == req.SessionId)
                          ?? StreamingVoiceCallSession.Create(req.TenantId, "CA_MOCK", "MZ_MOCK");

            var result = transferService.InitiateWarmSipTransfer(session, req.TargetSipUri, req.Reason, req.Summary);
            db.SaveChanges();

            return Results.Ok(result);
        });

        // Voice Realtime Bridge & Softphone
        app.MapPost("/api/voice/realtime/session-config", ([FromBody] RealtimeSessionConfig config, OpenAiRealtimeBridgeService bridge) =>
        {
            var sessionCmd = bridge.BuildSessionUpdateCommand(config);
            return Results.Ok(new { commandJson = sessionCmd });
        });

        app.MapGet("/api/voice/softphone/config", ([FromQuery] Guid tenantId, [FromQuery] Guid userId, BrowserSoftphoneSignalingService softphone) =>
        {
            var config = softphone.CreateSessionConfig(tenantId, userId);
            return Results.Ok(config);
        });

        // Autonomous Voice AI Dialer
        app.MapPost("/api/voice/dialer/queue", async ([FromBody] QueueVoiceOrderDto req, IAutomatedVoiceDialerService dialerService) =>
        {
            var attempt = await dialerService.QueueOrderForVoiceConfirmationAsync(req.TenantId, req.OrderId, req.CampaignId);
            return Results.Ok(attempt);
        });

        app.MapPost("/api/voice/dialer/process-turn", async ([FromBody] ProcessVoiceTurnDto req, IAutomatedVoiceDialerService dialerService) =>
        {
            var attempt = await dialerService.ProcessIvrCustomerResponseAsync(req.TenantId, req.AttemptId, req.SpeechText, req.DtmfDigit);
            return Results.Ok(attempt);
        });

        return app;
    }
}
