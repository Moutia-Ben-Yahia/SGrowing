using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Aerospace.RtgDecayThermocouple;

public static class RtgDecayEndpoints
{
    public static IServiceCollection AddRtgDecayThermocoupleServices(this IServiceCollection services)
    {
        services.AddScoped<IRtgDecayThermocoupleEngine, RtgDecayThermocoupleEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRtgDecayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/rtg-decay")
            .WithTags("Deep-Space RTG Pu-238 Decay & Thermocouple Power");

        group.MapPost("/evaluate", (
            [FromBody] RtgEvaluationRequest request,
            [FromServices] IRtgDecayThermocoupleEngine engine) =>
        {
            var assessment = engine.EvaluateRtgPower(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateRtgPowerDecay")
        .WithSummary("Calculates Pu-238 isotopic half-life decay, thermocouple efficiency loss, 10-year projection, and emergency load shedding.");

        group.MapGet("/telemetry/{rtgId}", (string rtgId) =>
        {
            var nominal = new RtgHardwareTelemetry(rtgId);
            return Results.Ok(new
            {
                RtgId = rtgId,
                Thermocouple = nominal.ThermocoupleMaterial,
                InitialThermalWatts = nominal.InitialThermalPowerWatts,
                YearsInFlight = nominal.YearsSinceFuelEncapsulation,
                BusVoltage = nominal.CurrentBusVoltageVolts,
                Status = "RADIOGENIC_HEAT_SOURCE_ACTIVE"
            });
        })
        .WithName("GetRtgHardwareTelemetry")
        .WithSummary("Returns hardware configuration, flight age, and thermocouple material for a deep space RTG power unit.");

        return app;
    }
}
