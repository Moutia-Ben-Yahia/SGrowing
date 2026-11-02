namespace SGrowing.Api.Modules.Telecom.SubseaAcousticEarlyWarning;

public static class SubseaAcousticEndpoints
{
    public static IServiceCollection AddSubseaAcousticServices(this IServiceCollection services)
    {
        services.AddSingleton<SubseaAcousticEarlyWarningEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSubseaAcousticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/telecom/subsea-acoustic-warning")
            .WithTags("Subsea Fiber Acoustic Early Warning");

        group.MapPost("/analyze-readings", (SubseaAcousticAnalysisRequest request, SubseaAcousticEarlyWarningEngine engine) =>
        {
            var result = engine.AnalyzeReadings(request);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSubseaAcoustics")
        .WithSummary("Processes Distributed Acoustic Sensing (DAS) strain data along submarine fiber cables to detect seismic ruptures and anchor dragging hazards");

        return app;
    }
}
