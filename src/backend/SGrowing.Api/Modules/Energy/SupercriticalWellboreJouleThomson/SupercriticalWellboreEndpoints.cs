namespace SGrowing.Api.Modules.Energy.SupercriticalWellboreJouleThomson;

public static class SupercriticalWellboreEndpoints
{
    public static IServiceCollection AddSupercriticalWellboreServices(this IServiceCollection services)
    {
        services.AddScoped<SupercriticalWellboreEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSupercriticalWellboreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/supercritical-wellbore")
            .WithTags("Deep Geothermal Supercritical Wellbore Thermohydraulic Arbiter");

        group.MapPost("/analyze", (SupercriticalWellboreTelemetry telemetry, SupercriticalWellboreEngine engine) =>
        {
            var result = engine.AnalyzeWellbore(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSupercriticalWellbore")
        .WithSummary("Analyzes deep supercritical geothermal co-axial wellbores, natural thermosiphon buoyancy driving pressure, and Joule-Thomson choke cooling.");

        return endpoints;
    }
}
