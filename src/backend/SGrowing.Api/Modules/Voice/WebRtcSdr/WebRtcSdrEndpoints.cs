namespace SGrowing.Api.Modules.Voice.WebRtcSdr;

public static class WebRtcSdrEndpoints
{
    public static IServiceCollection AddWebRtcSdrServices(this IServiceCollection services)
    {
        services.AddSingleton<IWebRtcSdrEngine, WebRtcSdrEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWebRtcSdrEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/sdr")
            .WithTags("WebRTC Voice SDR");

        group.MapPost("/sessions", (IWebRtcSdrEngine engine, StartVoiceSessionRequest request) =>
        {
            var session = engine.StartSession(request);
            return Results.Created($"/api/v1/voice/sdr/sessions/{session.SessionId}", session);
        })
        .WithName("StartVoiceSession")
        .WithSummary("Initiate an ultra-low latency full-duplex WebRTC Voice SDR session");

        group.MapGet("/sessions/{sessionId}", (IWebRtcSdrEngine engine, string sessionId) =>
        {
            var session = engine.GetSession(sessionId);
            return session is not null ? Results.Ok(session) : Results.NotFound(new { error = $"Session '{sessionId}' not found" });
        })
        .WithName("GetVoiceSession")
        .WithSummary("Retrieve live telemetry, VAD status, and conversation transcript for a voice call");

        group.MapPost("/sessions/{sessionId}/audio-stream", (IWebRtcSdrEngine engine, string sessionId, AudioStreamPacket packet) =>
        {
            try
            {
                var adjustedPacket = packet with { SessionId = sessionId };
                var response = engine.ProcessAudioPacket(adjustedPacket);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ProcessAudioPacket")
        .WithSummary("Stream bidirectional audio chunks with realtime barge-in voice activity detection");

        group.MapPost("/sessions/{sessionId}/end", (IWebRtcSdrEngine engine, string sessionId, EndVoiceSessionRequest request) =>
        {
            try
            {
                var adjustedReq = request with { SessionId = sessionId };
                var ended = engine.EndSession(adjustedReq);
                return Results.Ok(ended);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("EndVoiceSession")
        .WithSummary("Conclude the voice call session, capture call disposition, and commit transcript");

        group.MapGet("/sessions", (IWebRtcSdrEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var sessions = engine.ListSessions(targetTenant);
            return Results.Ok(sessions);
        })
        .WithName("ListVoiceSessions")
        .WithSummary("List all historical and active voice SDR sessions for the tenant");

        return app;
    }
}
