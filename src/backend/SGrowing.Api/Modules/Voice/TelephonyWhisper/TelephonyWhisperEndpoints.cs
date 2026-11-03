namespace SGrowing.Api.Modules.Voice.TelephonyWhisper;

public static class TelephonyWhisperEndpoints
{
    public static IServiceCollection AddTelephonyWhisperServices(this IServiceCollection services)
    {
        services.AddSingleton<TelephonyWhisperCoachEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTelephonyWhisperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/telephony-whisper")
            .WithTags("Voice - Real-Time Telephony Whisper Coach & Supervisory Takeover");

        group.MapGet("/overview", (TelephonyWhisperCoachEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/inject-whisper", (TelephonyWhisperCoachEngine engine, InjectWhisperRequest request) =>
        {
            var record = engine.InjectWhisper(request);
            return Results.Created($"/api/v1/voice/telephony-whisper/whispers/{record.WhisperId}", record);
        });

        group.MapPost("/takeover", (TelephonyWhisperCoachEngine engine, TakeoverCallRequest request) =>
        {
            var session = engine.TransitionSupervisionMode(request);
            return Results.Ok(session);
        });

        return app;
    }
}
