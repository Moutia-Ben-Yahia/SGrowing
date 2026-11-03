namespace SGrowing.Api.Modules.Voice.TelephonyDisfluency;

public static class TelephonyDisfluencyEndpoints
{
    public static IServiceCollection AddTelephonyDisfluencyServices(this IServiceCollection services)
    {
        services.AddSingleton<TelephonyDisfluencyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTelephonyDisfluencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/disfluency")
            .WithTags("Voice - Telephony Silence & Disfluency Diagnostic Neural Mesh");

        group.MapGet("/overview", (TelephonyDisfluencyEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/analyze-cadence", (TelephonyDisfluencyEngine engine, AnalyzeCadenceRequest request) =>
        {
            var evt = engine.AnalyzeCadence(request);
            return Results.Created($"/api/v1/voice/disfluency/events/{evt.EventId}", evt);
        });

        group.MapPost("/tune-pacing", (TelephonyDisfluencyEngine engine, TunePacingRequest request) =>
        {
            var profile = engine.TunePacing(request);
            return Results.Ok(profile);
        });

        return app;
    }
}
