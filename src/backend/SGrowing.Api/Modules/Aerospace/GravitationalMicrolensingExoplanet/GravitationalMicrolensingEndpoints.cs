namespace SGrowing.Api.Modules.Aerospace.GravitationalMicrolensingExoplanet;

public static class GravitationalMicrolensingEndpoints
{
    public static IServiceCollection AddGravitationalMicrolensingServices(this IServiceCollection services)
    {
        services.AddScoped<GravitationalMicrolensingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGravitationalMicrolensingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/gravitational-microlensing")
            .WithTags("Gravitational Microlensing Exoplanet Arbiter");

        group.MapPost("/analyze", (MicrolensingEventTelemetry telemetry, GravitationalMicrolensingEngine engine) =>
        {
            var result = engine.AnalyzeEvent(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMicrolensingEvent")
        .WithSummary("Analyzes gravitational microlensing light curves, detects planetary caustic crossings, and coordinates robotic telescope follow-up alerts.");

        return endpoints;
    }
}
