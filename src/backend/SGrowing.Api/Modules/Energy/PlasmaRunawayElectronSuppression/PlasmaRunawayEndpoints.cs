namespace SGrowing.Api.Modules.Energy.PlasmaRunawayElectronSuppression;

public static class PlasmaRunawayEndpoints
{
    public static IServiceCollection AddPlasmaRunawaySuppressionServices(this IServiceCollection services)
    {
        services.AddScoped<PlasmaRunawaySuppressionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPlasmaRunawayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/plasma-runaway")
            .WithTags("Tokamak Plasma Runaway Electron Avalanche Suppression Arbiter");

        group.MapPost("/analyze", (PlasmaRunawayTelemetry telemetry, PlasmaRunawaySuppressionEngine engine) =>
        {
            var result = engine.AnalyzeDisruption(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzePlasmaRunawayDisruption")
        .WithSummary("Analyzes tokamak disruption current quench rates, relativistic runaway electron avalanches, and Shattered Pellet Injection (SPI) triggers.");

        return endpoints;
    }
}
