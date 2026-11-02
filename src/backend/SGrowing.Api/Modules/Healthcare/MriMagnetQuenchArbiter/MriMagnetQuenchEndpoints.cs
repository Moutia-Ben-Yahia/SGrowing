using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Healthcare.MriMagnetQuenchArbiter;

public static class MriMagnetQuenchEndpoints
{
    public static IServiceCollection AddMriMagnetQuenchServices(this IServiceCollection services)
    {
        services.AddScoped<IMriMagnetQuenchArbiterEngine, MriMagnetQuenchArbiterEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMriMagnetQuenchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/healthcare/mri-quench")
            .WithTags("MRI Magnet Quench Protection");

        group.MapPost("/evaluate", (
            [FromBody] MriMagnetQuenchRequest request,
            [FromServices] IMriMagnetQuenchArbiterEngine engine) =>
        {
            var decision = engine.EvaluateQuenchRisk(request);
            return Results.Ok(decision);
        })
        .WithName("EvaluateMriMagnetQuenchRisk")
        .WithSummary("Evaluates superconducting coil resistive transitions and helium boil-off for high-field MRI quench protection.");

        group.MapGet("/status/{scannerId}", (string scannerId) =>
        {
            var nominalState = new MriMagnetState(scannerId);
            return Results.Ok(new
            {
                ScannerId = scannerId,
                Status = "NOMINAL_SUPERCONDUCTING",
                ColdHeadKelvin = nominalState.ColdHeadTemperatureKelvin,
                MagneticFieldTesla = nominalState.MagneticFieldTesla,
                VacuumMbar = nominalState.CryostatVacuumPressureMbar
            });
        })
        .WithName("GetMriMagnetStatus")
        .WithSummary("Returns baseline cryogenic and magnetic telemetry for an MRI scanner.");

        return app;
    }
}
