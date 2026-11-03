using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.AudioNoiseCancellation;

public static class AudioNoiseCancellationEndpoints
{
    public static IServiceCollection AddAudioNoiseCancellationServices(this IServiceCollection services)
    {
        services.AddSingleton<IAudioNoiseCancellationEngine, AudioNoiseCancellationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAudioNoiseCancellationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/audio-noise-cancellation")
            .WithTags("Voice - Real-Time Audio Noise Cancellation & Speech Isolation DSP");

        group.MapGet("/overview", (IAudioNoiseCancellationEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetNoiseCancellationOverview")
        .WithSummary("Retrieves live audio channels, spectral noise floors, SNR metrics, and MOS voice clarity scores");

        group.MapPost("/filter", (ToggleNoiseFilterCommand command, IAudioNoiseCancellationEngine engine) =>
        {
            try
            {
                var result = engine.ToggleFilter(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Channel {command.ChannelId} not found." });
            }
        })
        .WithName("ToggleAudioNoiseFilter")
        .WithSummary("Enables or disables real-time sub-15ms spectral gating and speech isolation on WebRTC stream");

        return endpoints;
    }
}
