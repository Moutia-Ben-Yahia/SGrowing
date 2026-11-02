using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Aerospace.SpaceElevatorTether;

public static class SpaceElevatorTetherEndpoints
{
    public static IServiceCollection AddSpaceElevatorTetherServices(this IServiceCollection services)
    {
        services.AddScoped<ISpaceElevatorTetherEngine, SpaceElevatorTetherEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpaceElevatorTetherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/space-elevator")
            .WithTags("Space Elevator Tether Dynamic Stabilization");

        group.MapPost("/evaluate", (
            [FromBody] SpaceElevatorTetherRequest request,
            [FromServices] ISpaceElevatorTetherEngine engine) =>
        {
            var plan = engine.ComputeStabilization(request);
            return Results.Ok(plan);
        })
        .WithName("EvaluateSpaceElevatorStabilization")
        .WithSummary("Calculates Coriolis deflection, dynamic tension, and active counterweight thruster damping for space elevator CNT tethers.");

        group.MapGet("/tether/{tetherId}", (string tetherId) =>
        {
            var nominal = new TetherDynamicState(tetherId);
            return Results.Ok(new
            {
                TetherId = tetherId,
                LengthKm = nominal.TetherLengthKm,
                BaseAnchorTensionGpa = nominal.BaseAnchorTensionGigaPascals,
                GeoTensionGpa = nominal.GeoTensionGigaPascals,
                MaxTensionGpa = nominal.MaxAllowableTensionGigaPascals,
                Status = "ANCHORED_EQUATORIAL_PACIFIC"
            });
        })
        .WithName("GetSpaceElevatorTetherStatus")
        .WithSummary("Returns physical telemetry and structural limits for a carbon nanotube space elevator tether.");

        return app;
    }
}
