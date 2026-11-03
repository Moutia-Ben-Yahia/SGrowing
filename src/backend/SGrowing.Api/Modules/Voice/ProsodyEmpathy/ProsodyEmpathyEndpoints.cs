namespace SGrowing.Api.Modules.Voice.ProsodyEmpathy;

public static class ProsodyEmpathyEndpoints
{
    public static IServiceCollection AddProsodyEmpathyServices(this IServiceCollection services)
    {
        services.AddSingleton<IProsodyEmpathyEngine, ProsodyEmpathyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapProsodyEmpathyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/prosody-empathy")
            .WithTags("Voice - Speech Prosody & Emotional Empathy Tone Shifter");

        group.MapGet("/overview", (IProsodyEmpathyEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/shift-tone", (ShiftToneRequest req, IProsodyEmpathyEngine engine) =>
        {
            var evt = engine.ShiftToneForCaller(req.CallSessionId, req.CallerEmotion, req.Confidence);
            return Results.Ok(evt);
        });

        group.MapPost("/classify-emotion", (ClassifyEmotionRequest req, IProsodyEmpathyEngine engine) =>
        {
            var state = engine.ClassifyUtteranceEmotion(req.TranscriptSnippet, req.SpeechEnergyRms);
            return Results.Ok(new { EmotionState = state });
        });

        return endpoints;
    }
}

public sealed record ShiftToneRequest(string CallSessionId, CallerEmotionState CallerEmotion, double Confidence);
public sealed record ClassifyEmotionRequest(string TranscriptSnippet, double SpeechEnergyRms);
