namespace SGrowing.Api.Modules.Voice.VoiceBiometric;

public static class VoiceBiometricEndpoints
{
    public static IServiceCollection AddVoiceBiometricServices(this IServiceCollection services)
    {
        services.AddSingleton<VoiceBiometricDefenseEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVoiceBiometricEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/biometric-defense")
            .WithTags("Voice - Biometric Speaker Verification & Deepfake Defense");

        group.MapGet("/overview", (VoiceBiometricDefenseEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/verify-audio", (VoiceBiometricDefenseEngine engine, VerifyAudioFrameRequest request) =>
        {
            var audit = engine.VerifyAudio(request);
            return Results.Created($"/api/v1/voice/biometric-defense/audits/{audit.AuditId}", audit);
        });

        group.MapPost("/quarantine-call", (VoiceBiometricDefenseEngine engine, QuarantineCallRequest request) =>
        {
            var audit = engine.QuarantineCall(request);
            return Results.Created($"/api/v1/voice/biometric-defense/audits/{audit.AuditId}", audit);
        });

        return app;
    }
}
