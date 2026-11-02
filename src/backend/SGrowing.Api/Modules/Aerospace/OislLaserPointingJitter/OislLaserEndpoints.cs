namespace SGrowing.Api.Modules.Aerospace.OislLaserPointingJitter;

public static class OislLaserEndpoints
{
    public static IServiceCollection AddOislLaserServices(this IServiceCollection services)
    {
        services.AddScoped<OislLaserPointingJitterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOislLaserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/oisl-laser")
            .WithTags("LEO Optical Inter-Satellite Laser Link PAT Jitter Arbiter");

        group.MapPost("/analyze", (OislLaserTelemetry telemetry, OislLaserPointingJitterEngine engine) =>
        {
            var result = engine.AnalyzeLinkJitter(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeOislLaserJitter")
        .WithSummary("Analyzes LEO optical inter-satellite laser pointing jitter, beam divergence coupling loss, and Fast Steering Mirror PAT dynamics.");

        return endpoints;
    }
}
