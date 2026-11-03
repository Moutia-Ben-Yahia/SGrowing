namespace SGrowing.Api.Modules.Voice.VoiceIntentFastPath;

public static class VoiceIntentFastPathEndpoints
{
    public static IServiceCollection AddVoiceIntentFastPathServices(this IServiceCollection services)
    {
        services.AddSingleton<VoiceIntentFastPathEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVoiceIntentFastPathEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/intent-fast-path")
            .WithTags("Voice - Acoustic Normalization & Speech-to-Intent Fast-Path");

        group.MapGet("/overview", (VoiceIntentFastPathEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/classify-intent", (VoiceIntentFastPathEngine engine, ClassifyIntentRequest request) =>
        {
            var prediction = engine.ClassifyIntent(request);
            return Results.Created($"/api/v1/voice/intent-fast-path/predictions/{prediction.PredictionId}", prediction);
        });

        group.MapPost("/tune-threshold", (VoiceIntentFastPathEngine engine, TuneThresholdRequest request) =>
        {
            var threshold = engine.TuneThreshold(request);
            return Results.Ok(new { CutoffThreshold = threshold });
        });

        return app;
    }
}
