namespace SGrowing.Api.Modules.Advertising.AudioDai;

public static class AudioDaiEndpoints
{
    public static IServiceCollection AddAudioDaiServices(this IServiceCollection services)
    {
        services.AddSingleton<AudioDaiMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAudioDaiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/audio-dai")
            .WithTags("Advertising - Programmatic Audio Dynamic Ad Insertion");

        group.MapGet("/overview", (AudioDaiMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/slots/insert", (AudioDaiMeshEngine engine, InsertAudioAdRequest request) =>
        {
            var slot = engine.InsertAudioSlot(request);
            return Results.Created($"/api/v1/advertising/audio-dai/slots/{slot.SlotId}", slot);
        });

        group.MapPost("/attribution/record", (AudioDaiMeshEngine engine, RecordAudioAttributionRequest request) =>
        {
            var attribution = engine.RecordAttribution(request);
            return Results.Created($"/api/v1/advertising/audio-dai/attribution/{attribution.AttributionId}", attribution);
        });

        return app;
    }
}
