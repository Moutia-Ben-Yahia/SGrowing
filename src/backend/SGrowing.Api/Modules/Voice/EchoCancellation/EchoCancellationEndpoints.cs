namespace SGrowing.Api.Modules.Voice.EchoCancellation;

public static class EchoCancellationEndpoints
{
    public static IServiceCollection AddEchoCancellationServices(this IServiceCollection services)
    {
        services.AddSingleton<EchoCancellationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEchoCancellationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/echo-cancellation")
            .WithTags("Voice - Neural Acoustic Echo Cancellation & Double-Talk Arbiter");

        group.MapGet("/overview", (EchoCancellationEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/process-frame", (EchoCancellationEngine engine, ProcessAudioFrameRequest request) =>
        {
            var frame = engine.ProcessAudioFrame(request);
            return Results.Created($"/api/v1/voice/echo-cancellation/frames/{frame.FrameId}", frame);
        });

        group.MapPost("/tune-suppression", (EchoCancellationEngine engine, TuneSuppressionRequest request) =>
        {
            var profile = engine.TuneSuppression(request);
            return Results.Ok(profile);
        });

        return app;
    }
}
