using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.VoiceLiveness;

public static class VoiceLivenessEndpoints
{
    public static IServiceCollection AddVoiceLivenessServices(this IServiceCollection services)
    {
        services.AddSingleton<SpectralAcousticLivenessAnalyzer>();
        services.AddSingleton<BiometricChallengeResponseService>();
        services.AddSingleton<SyntheticVoiceRiskScorer>();
        return services;
    }

    public static IEndpointRouteBuilder MapVoiceLivenessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/voice-liveness")
            .WithTags("Real-Time Audio Deepfake & Biometric Voice Spoofing Defense");

        group.MapPost("/evaluate", (
            VerifyVoiceLivenessRequest request,
            SyntheticVoiceRiskScorer scorer) =>
        {
            var result = scorer.EvaluateLiveness(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateVoiceLiveness")
        .WithSummary("Analyzes audio samples for neural vocoder artifacts, HNR resonance, and biometric challenge compliance.");

        group.MapPost("/challenges", (
            IssueChallengeRequest request,
            BiometricChallengeResponseService challengeService) =>
        {
            var challenge = challengeService.IssueChallenge(request);
            return Results.Created($"/api/v1/security/voice-liveness/challenges/{challenge.ChallengeId}", challenge);
        })
        .WithName("IssueBiometricChallenge")
        .WithSummary("Generates a dynamic randomized phonetic challenge phrase for an active call session.");

        group.MapPost("/challenges/verify", (
            string challengeId,
            int latencyMs,
            BiometricChallengeResponseService challengeService) =>
        {
            var (passed, reason) = challengeService.EvaluateChallengeResponse(challengeId, latencyMs);
            return Results.Ok(new { Passed = passed, Reason = reason });
        })
        .WithName("VerifyChallengeResponse")
        .WithSummary("Verifies caller reaction latency to detect automated soundboards or speech-to-speech AI delays.");

        return app;
    }
}
