using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Voice.VocalTremor;

public static class VocalTremorEndpoints
{
    public static IServiceCollection AddVocalTremorServices(this IServiceCollection services)
    {
        services.AddSingleton<IVocalTremorEngine, VocalTremorEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVocalTremorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/vocal-tremor")
            .WithTags("Voice Whisper Isolation & Micro-Vocal Tremor Detection");

        group.MapPost("/whisper-boost", (
            [FromQuery] string callId,
            [FromQuery] double energyDbfs,
            [FromServices] IVocalTremorEngine engine) =>
        {
            var result = engine.ProcessWhisperEnhancement(callId, energyDbfs);
            return Results.Ok(result);
        })
        .WithName("ProcessWhisperEnhancement")
        .WithSummary("Detects whisper speech dynamics and applies formant boost filtering");

        group.MapPost("/analyze", (
            [FromBody] AnalyzeAcousticsRequest request,
            [FromServices] IVocalTremorEngine engine) =>
        {
            var metrics = engine.AnalyzeAcoustics(request);
            return Results.Ok(metrics);
        })
        .WithName("AnalyzeAcousticTremor")
        .WithSummary("Analyzes 8-14Hz micro-vocal cord tremors and computes acute stress index");

        group.MapGet("/sessions", (
            [FromServices] IVocalTremorEngine engine) =>
        {
            var sessions = engine.GetSessionSummaries();
            return Results.Ok(sessions);
        })
        .WithName("GetVocalTremorSessions")
        .WithSummary("Retrieves all audited vocal stress and whisper isolation call sessions");

        group.MapGet("/sessions/{callId}", (
            string callId,
            [FromServices] IVocalTremorEngine engine) =>
        {
            var session = engine.GetSessionSummary(callId);
            return session is not null ? Results.Ok(session) : Results.NotFound();
        })
        .WithName("GetVocalTremorSessionDetail")
        .WithSummary("Fetches session details for an individual vocal call audit");

        return app;
    }
}
