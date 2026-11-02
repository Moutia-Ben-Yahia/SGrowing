namespace SGrowing.Api.Modules.Automotive.TeleoperationFoveatedStream;

public static class TeleoperationFoveatedEndpoints
{
    public static IServiceCollection AddTeleoperationFoveatedServices(this IServiceCollection services)
    {
        services.AddSingleton<TeleoperationFoveatedStreamEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTeleoperationFoveatedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/automotive/teleoperation-foveated-stream")
            .WithTags("AV Teleoperation Foveated Transcoder");

        group.MapPost("/transcode-frame", (FoveatedTranscodeRequest request, TeleoperationFoveatedStreamEngine engine) =>
        {
            var result = engine.TranscodeFoveatedFrame(request);
            return Results.Ok(result);
        })
        .WithName("TranscodeFoveatedFrame")
        .WithSummary("Encodes AV remote teleoperation video stream using gaze-contingent foveated QP gradients under 5G network latency budgets");

        return app;
    }
}
