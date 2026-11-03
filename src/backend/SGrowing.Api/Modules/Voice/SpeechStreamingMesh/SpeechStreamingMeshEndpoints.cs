using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.SpeechStreamingMesh;

public static class SpeechStreamingMeshEndpoints
{
    public static IServiceCollection AddSpeechStreamingMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<SpeechStreamingMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpeechStreamingMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/speech-streaming-mesh")
            .WithTags("Voice - Speech-to-Speech (S2S) Ultra-Low-Latency Audio Streaming Mesh");

        group.MapGet("/overview", (SpeechStreamingMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetSpeechStreamingMeshOverview")
        .WithSummary("Retrieve active full-duplex S2S streams, P99 audio latency, and barge-in metrics");

        group.MapPost("/process-frame", (ProcessAudioChunkRequest request, SpeechStreamingMeshEngine engine) =>
        {
            var result = engine.ProcessAudioChunk(request);
            return Results.Ok(result);
        })
        .WithName("ProcessSpeechAudioChunk")
        .WithSummary("Ingest raw audio frame and return synthesized S2S neural audio chunk with sub-80ms latency");

        group.MapPost("/trigger-barge-in", (TriggerBargeInRequest request, SpeechStreamingMeshEngine engine) =>
        {
            var receipt = engine.TriggerBargeIn(request);
            return Results.Ok(receipt);
        })
        .WithName("TriggerSpeechBargeIn")
        .WithSummary("Immediately dampen active TTS playback upon user speech onset");

        return endpoints;
    }
}
