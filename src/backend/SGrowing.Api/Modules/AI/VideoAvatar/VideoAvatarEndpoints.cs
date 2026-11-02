using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.VideoAvatar;

public static class VideoAvatarEndpoints
{
    public static IServiceCollection AddVideoAvatarServices(this IServiceCollection services)
    {
        services.AddSingleton<VideoAvatarEngine>();
        return services;
    }

    public static RouteGroupBuilder MapVideoAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/avatar")
            .WithTags("AiVideoAvatar");

        group.MapGet("/personas", async Task<Ok<IReadOnlyList<AvatarPersona>>> (
            [FromServices] VideoAvatarEngine engine,
            CancellationToken ct) =>
        {
            var personas = await engine.GetAvailablePersonasAsync(ct);
            return TypedResults.Ok(personas);
        })
        .WithName("GetAvatarPersonas")
        .WithSummary("Retrieve available AI Video Avatar personas");

        group.MapPost("/sessions", async Task<Results<Ok<AvatarSessionState>, BadRequest<string>>> (
            [FromBody] AvatarSessionInitRequest request,
            [FromServices] VideoAvatarEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var session = await engine.InitializeSessionAsync(request, ct);
                return TypedResults.Ok(session);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("InitializeAvatarSession")
        .WithSummary("Initialize WebRTC streaming session with an interactive avatar");

        group.MapPost("/sessions/{id}/speak", async Task<Results<Ok<AvatarSpeechSynthesisResponse>, NotFound<string>>> (
            [FromRoute] string id,
            [FromBody] AvatarSpeechRequest request,
            [FromServices] VideoAvatarEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var response = await engine.SynthesizeSpeechAndVisemesAsync(id, request, ct);
                return TypedResults.Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Avatar session '{id}' not found.");
            }
        })
        .WithName("AvatarSpeak")
        .WithSummary("Synthesize speech audio and synchronized facial blendshape visemes");

        group.MapPost("/sessions/{id}/slides", async Task<Results<Ok<PresentationSlideEvent>, NotFound<string>>> (
            [FromRoute] string id,
            [FromBody] TriggerSlideRequest request,
            [FromServices] VideoAvatarEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var slideEvent = await engine.TriggerPresentationSlideAsync(
                    id,
                    request.SlideIndex,
                    request.SlideTitle,
                    request.MediaUrl,
                    request.Caption,
                    ct
                );
                return TypedResults.Ok(slideEvent);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Avatar session '{id}' not found.");
            }
        })
        .WithName("TriggerAvatarSlide")
        .WithSummary("Broadcast synchronized presentation slide during avatar interaction");

        group.MapGet("/sessions/{id}", async Task<Results<Ok<AvatarSessionState>, NotFound<string>>> (
            [FromRoute] string id,
            [FromServices] VideoAvatarEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var state = await engine.GetSessionStateAsync(id, ct);
                return TypedResults.Ok(state);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Avatar session '{id}' not found.");
            }
        })
        .WithName("GetAvatarSessionState")
        .WithSummary("Get active status and WebRTC telemetry of an avatar session");

        group.MapDelete("/sessions/{id}", async Task<Ok<bool>> (
            [FromRoute] string id,
            [FromServices] VideoAvatarEngine engine,
            CancellationToken ct) =>
        {
            var result = await engine.TerminateSessionAsync(id, ct);
            return TypedResults.Ok(result);
        })
        .WithName("TerminateAvatarSession")
        .WithSummary("Close WebRTC avatar streaming session");

        return group;
    }
}

public record TriggerSlideRequest(
    int SlideIndex,
    string SlideTitle,
    string MediaUrl,
    string Caption
);
