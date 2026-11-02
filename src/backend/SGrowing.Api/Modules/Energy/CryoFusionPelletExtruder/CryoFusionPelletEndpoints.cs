using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Energy.CryoFusionPelletExtruder;

public static class CryoFusionPelletEndpoints
{
    public static IServiceCollection AddCryoFusionPelletExtruderServices(this IServiceCollection services)
    {
        services.AddScoped<ICryoFusionPelletExtrusionEngine, CryoFusionPelletExtrusionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCryoFusionPelletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/fusion-pellet-extruder")
            .WithTags("Cryogenic Fusion D-T Pellet Extrusion & Fueling");

        group.MapPost("/evaluate", (
            [FromBody] PelletExtrusionEvaluationRequest request,
            [FromServices] ICryoFusionPelletExtrusionEngine engine) =>
        {
            var assessment = engine.EvaluateExtrusion(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateFusionPelletExtrusion")
        .WithSummary("Calculates D-T fueling mass rate, core plasma separatrix penetration depth, thermal sublimation, and mechanical jam risk.");

        group.MapGet("/barrel/{barrelId}", (string barrelId) =>
        {
            var nominal = new ExtruderCryostatTelemetry(barrelId);
            return Results.Ok(new
            {
                BarrelId = barrelId,
                TemperatureKelvin = nominal.IceBarrelTemperatureKelvin,
                PelletDiameterMm = nominal.PelletDiameterMm,
                MuzzleVelocityMPerSec = nominal.GasGunMuzzleVelocityMPerSec,
                Status = "SOLID_ICE_BARREL_CRYOGENIC"
            });
        })
        .WithName("GetFusionPelletBarrelStatus")
        .WithSummary("Returns temperature and pellet geometry for a fusion cryogenic D-T ice extruder barrel.");

        return app;
    }
}
