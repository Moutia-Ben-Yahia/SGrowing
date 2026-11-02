using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Aerospace.HallThrusterPoleErosion;

public static class HallThrusterEndpoints
{
    public static IServiceCollection AddHallThrusterPoleErosionServices(this IServiceCollection services)
    {
        services.AddScoped<IHallThrusterPoleErosionEngine, HallThrusterPoleErosionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHallThrusterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/hall-thruster")
            .WithTags("Hall Effect Thruster Anomaly & Pole Erosion");

        group.MapPost("/evaluate", (
            [FromBody] HallThrusterEvaluationRequest request,
            [FromServices] IHallThrusterPoleErosionEngine engine) =>
        {
            var assessment = engine.EvaluateThruster(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateHallThrusterHealth")
        .WithSummary("Calculates thrust, specific impulse, pole-piece sputtering erosion, and remaining operational firing life for Hall thrusters.");

        group.MapGet("/telemetry/{thrusterId}", (string thrusterId) =>
        {
            var nominal = new HallThrusterTelemetry(thrusterId);
            return Results.Ok(new
            {
                ThrusterId = thrusterId,
                Propellant = nominal.PropellantType,
                DischargeVoltage = nominal.AnodeDischargeVoltageVolts,
                MassFlowMgS = nominal.PropellantMassFlowRateMgPerSec,
                Status = "PLASMA_CHANNEL_IGNITED"
            });
        })
        .WithName("GetHallThrusterTelemetry")
        .WithSummary("Returns discharge voltage, propellant mass flow, and ignition status for an electric propulsion Hall thruster.");

        return app;
    }
}
