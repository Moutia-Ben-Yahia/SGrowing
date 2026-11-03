using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.MultilingualVoiceMesh;

public static class MultilingualVoiceMeshEndpoints
{
    public static IServiceCollection AddMultilingualVoiceMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<MultilingualVoiceMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMultilingualVoiceMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/multilingual-voice-mesh")
            .WithTags("Voice - Simultaneous Multilingual Speech Translation Mesh");

        group.MapGet("/overview", (MultilingualVoiceMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetMultilingualVoiceOverview")
        .WithSummary("Retrieve live cross-lingual translation streams, latency, and timbre similarity metrics");

        group.MapPost("/translate-audio-chunk", (TranslateAudioChunkRequest request, MultilingualVoiceMeshEngine engine) =>
        {
            var result = engine.TranslateAudioChunk(request);
            return Results.Ok(result);
        })
        .WithName("TranslateVoiceAudioChunk")
        .WithSummary("Translate streaming audio frame into target language with preserved speaker vocal timbre");

        group.MapPost("/register-voice-clone", (RegisterVoiceCloneRequest request, MultilingualVoiceMeshEngine engine) =>
        {
            var receipt = engine.RegisterVoiceClone(request);
            return Results.Ok(receipt);
        })
        .WithName("RegisterVoiceTimbreClone")
        .WithSummary("Extract zero-shot acoustic timbre embedding from reference audio sample");

        return endpoints;
    }
}
