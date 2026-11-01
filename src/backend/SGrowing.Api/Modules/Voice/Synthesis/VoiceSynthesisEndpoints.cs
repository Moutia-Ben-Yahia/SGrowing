using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.Synthesis;

/// <summary>
/// Endpoints and dependency injection extensions for autonomous real-time voice synthesis,
/// neural dialect cloning, and acoustic canary watermarking.
/// </summary>
public static class VoiceSynthesisEndpoints
{
    /// <summary>
    /// Registers all singleton voice synthesis and watermarking domain services in the DI container.
    /// </summary>
    public static IServiceCollection AddVoiceSynthesisServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAcousticCanaryWatermarkingService, AcousticCanaryWatermarkingService>();
        services.AddSingleton<IAcousticSentimentControllerService, AcousticSentimentControllerService>();
        services.AddSingleton<INeuralDialectSynthesizerEngine, NeuralDialectSynthesizerEngine>();

        return services;
    }

    /// <summary>
    /// Maps voice synthesis, dialect cloning, and watermark verification endpoints under /api/v1/voice/synthesis.
    /// </summary>
    public static IEndpointRouteBuilder MapVoiceSynthesisEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/voice/synthesis")
            .WithTags("VoiceSynthesis");

        // POST /api/v1/voice/synthesis/synthesize
        group.MapPost("/synthesize", (
            [FromBody] VoiceSynthesisRequest request,
            INeuralDialectSynthesizerEngine synthesizer) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(new { error = "Speech text cannot be null or empty." });
            }

            var result = synthesizer.SynthesizeSpeech(request);

            var response = new SynthesizeAudioResponse(
                AudioBase64: Convert.ToBase64String(result.AudioBuffer),
                DurationMs: result.DurationMs,
                SampleRateHz: result.SampleRateHz,
                Codec: result.Codec.ToString(),
                WatermarkSignatureHex: result.WatermarkSignatureHex,
                PhonemeCount: result.PhonemeCount,
                Dialect: result.Dialect.ToString(),
                CreatedAtUtc: result.CreatedAtUtc
            );

            return Results.Ok(response);
        })
        .WithName("SynthesizeSpeechWithDialect")
        .WithSummary("Synthesizes multilingual speech with regional dialect prosody and psychoacoustic watermarking.");

        // POST /api/v1/voice/synthesis/verify-watermark
        group.MapPost("/verify-watermark", (
            [FromBody] VerifyWatermarkRequest request,
            IAcousticCanaryWatermarkingService watermarking) =>
        {
            byte[]? audioBytes = request.AudioBytes;

            if ((audioBytes == null || audioBytes.Length == 0) && !string.IsNullOrWhiteSpace(request.AudioBase64))
            {
                try
                {
                    audioBytes = Convert.FromBase64String(request.AudioBase64);
                }
                catch (FormatException)
                {
                    return Results.BadRequest(new { error = "Invalid Base64 audio string supplied." });
                }
            }

            if (audioBytes == null || audioBytes.Length == 0)
            {
                return Results.BadRequest(new { error = "Either AudioBytes or AudioBase64 must be provided for watermark verification." });
            }

            var result = watermarking.ExtractAndVerifyWatermark(audioBytes, request.SampleRateHz, request.SecretKey ?? string.Empty);
            return Results.Ok(result);
        })
        .WithName("VerifyAcousticCanaryWatermark")
        .WithSummary("Extracts and cryptographically validates an acoustic canary watermark from an audio buffer.");

        // POST /api/v1/voice/synthesis/prosody-profile
        group.MapPost("/prosody-profile", (
            [FromBody] AnalyzeSentimentRequest request,
            IAcousticSentimentControllerService sentimentController) =>
        {
            if (request.Input == null)
            {
                return Results.BadRequest(new { error = "Acoustic sentiment input parameters are required." });
            }

            var analysis = sentimentController.AnalyzeAcousticSentiment(request.Input);
            return Results.Ok(analysis);
        })
        .WithName("AnalyzeAcousticProsodyProfile")
        .WithSummary("Analyzes acoustic voice parameters and determines de-escalating prosody modulation.");

        // GET /api/v1/voice/synthesis/accents
        group.MapGet("/accents", (
            INeuralDialectSynthesizerEngine synthesizer) =>
        {
            var accents = synthesizer.GetSupportedAccents();
            return Results.Ok(accents);
        })
        .WithName("ListSupportedDialectAccents")
        .WithSummary("Lists all supported Maghrebi regional dialect accents and their acoustic traits.");

        return app;
    }
}
