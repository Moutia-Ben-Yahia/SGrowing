namespace SGrowing.Api.Modules.Energy.GeothermalMicroSeismicity;

public static class GeothermalEndpoints
{
    public static IServiceCollection AddGeothermalMicroSeismicityServices(this IServiceCollection services)
    {
        services.AddSingleton<GeothermalMicroSeismicityArbiterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapGeothermalMicroSeismicityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/geothermal-microseismicity")
            .WithTags("Geothermal Micro-Seismicity Arbiter");

        group.MapPost("/evaluate-seismicity", (GeothermalSeismicAnalysisRequest request, GeothermalMicroSeismicityArbiterEngine engine) =>
        {
            var result = engine.EvaluateReservoirSeismicity(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateGeothermalSeismicity")
        .WithSummary("Monitors induced micro-seismic events in enhanced geothermal reservoirs, enforces Traffic Light System (TLS) protocols, and throttles injection pumps");

        return app;
    }
}
