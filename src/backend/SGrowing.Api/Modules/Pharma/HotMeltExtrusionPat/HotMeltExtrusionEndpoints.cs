namespace SGrowing.Api.Modules.Pharma.HotMeltExtrusionPat;

public static class HotMeltExtrusionEndpoints
{
    public static IServiceCollection AddHotMeltExtrusionServices(this IServiceCollection services)
    {
        services.AddSingleton<HotMeltExtrusionPatEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHotMeltExtrusionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pharma/hot-melt-extrusion-pat")
            .WithTags("Pharma Hot-Melt Extrusion PAT");

        group.MapPost("/evaluate-stream", (HmeBatchMonitoringRequest request, HotMeltExtrusionPatEngine engine) =>
        {
            var result = engine.EvaluateExtrusionStream(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateHmeExtrusionStream")
        .WithSummary("Monitors continuous pharmaceutical hot-melt extrusion, in-line NIR API concentration, and actuates out-of-spec divert gates");

        return app;
    }
}
