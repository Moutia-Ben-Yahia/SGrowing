namespace SGrowing.Api.Modules.Aerospace.VasimrPlasmaPropulsion;

public static class VasimrPlasmaEndpoints
{
    public static IServiceCollection AddVasimrPlasmaPropulsionServices(this IServiceCollection services)
    {
        services.AddScoped<VasimrPlasmaPropulsionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVasimrPlasmaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/vasimr-propulsion")
            .WithTags("Satellite Drag Makeup & VASIMR Plasma Propulsion Arbiter");

        group.MapPost("/analyze", (VasimrPlasmaTelemetry telemetry, VasimrPlasmaPropulsionEngine engine) =>
        {
            var result = engine.AnalyzePropulsion(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeVasimrPropulsion")
        .WithSummary("Analyzes VLEO atmospheric aerodynamic drag, VASIMR variable Isp and thrust modulation, and orbit lifetime preservation.");

        return endpoints;
    }
}
