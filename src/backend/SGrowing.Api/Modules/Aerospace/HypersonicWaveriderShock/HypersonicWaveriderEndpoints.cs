using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Aerospace.HypersonicWaveriderShock;

public static class HypersonicWaveriderEndpoints
{
    public static IServiceCollection AddHypersonicWaveriderShockServices(this IServiceCollection services)
    {
        services.AddScoped<IHypersonicWaveriderShockEngine, HypersonicWaveriderShockEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHypersonicWaveriderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/hypersonic-waverider")
            .WithTags("Hypersonic Waverider Aerothermal Shock Arbiter");

        group.MapPost("/evaluate", (
            [FromBody] HypersonicWaveriderEvaluationRequest request,
            [FromServices] IHypersonicWaveriderShockEngine engine) =>
        {
            var plan = engine.EvaluateAerothermalState(request);
            return Results.Ok(plan);
        })
        .WithName("EvaluateHypersonicWaveriderAerothermal")
        .WithSummary("Calculates oblique shock attachment, lift-to-drag ratio, stagnation heat flux, and active transpiration cooling flow rates.");

        group.MapGet("/status/{vehicleId}", (string vehicleId) =>
        {
            var nominal = new WaveriderFlightState(vehicleId);
            return Results.Ok(new
            {
                VehicleId = vehicleId,
                MachNumber = nominal.MachNumber,
                AltitudeKm = nominal.AltitudeKm,
                DynamicPressureKPa = nominal.DynamicPressureKPa,
                Status = "HYPERSONIC_CORRIDOR_TRACKING"
            });
        })
        .WithName("GetHypersonicWaveriderStatus")
        .WithSummary("Returns Mach number, altitude, and dynamic pressure telemetry for a sub-orbital hypersonic waverider vehicle.");

        return app;
    }
}
