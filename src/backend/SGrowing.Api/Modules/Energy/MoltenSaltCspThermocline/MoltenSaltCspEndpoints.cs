using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Energy.MoltenSaltCspThermocline;

public static class MoltenSaltCspEndpoints
{
    public static IServiceCollection AddMoltenSaltCspThermoclineServices(this IServiceCollection services)
    {
        services.AddScoped<IMoltenSaltCspThermoclineEngine, MoltenSaltCspThermoclineEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMoltenSaltCspEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/csp-thermocline")
            .WithTags("Molten Salt CSP Thermocline Thermal Storage");

        group.MapPost("/evaluate", (
            [FromBody] ThermoclineEvaluationRequest request,
            [FromServices] IMoltenSaltCspThermoclineEngine engine) =>
        {
            var plan = engine.EvaluateThermocline(request);
            return Results.Ok(plan);
        })
        .WithName("EvaluateCspThermocline")
        .WithSummary("Calculates stored MWh, thermal capacity utilization, thermocline thickness, and freeze protection for molten salt CSP tanks.");

        group.MapGet("/tank/{tankId}", (string tankId) =>
        {
            var nominal = new ThermoclineTankTelemetry(tankId);
            return Results.Ok(new
            {
                TankId = tankId,
                HeightMeters = nominal.TankHeightMeters,
                DiameterMeters = nominal.TankDiameterMeters,
                HotZoneCelsius = nominal.HotZoneTemperatureCelsius,
                ColdZoneCelsius = nominal.ColdZoneTemperatureCelsius,
                Status = "PACKED_BED_STRATIFIED"
            });
        })
        .WithName("GetCspThermoclineTankTelemetry")
        .WithSummary("Returns dimensional and operational temperature profile for a concentrated solar power thermocline tank.");

        return app;
    }
}
