using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Energy.FusionDivertorDetachment;

public static class FusionDivertorEndpoints
{
    public static IServiceCollection AddFusionDivertorDetachmentServices(this IServiceCollection services)
    {
        services.AddScoped<IFusionDivertorDetachmentEngine, FusionDivertorDetachmentEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFusionDivertorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/divertor-detachment")
            .WithTags("Fusion Tokamak Divertor Detachment & Impurity Seeding");

        group.MapPost("/evaluate", (
            [FromBody] DivertorDetachmentRequest request,
            [FromServices] IFusionDivertorDetachmentEngine engine) =>
        {
            var decision = engine.EvaluateDetachment(request);
            return Results.Ok(decision);
        })
        .WithName("EvaluateDivertorDetachment")
        .WithSummary("Calculates degree of detachment, target heat flux, and automated nitrogen/argon impurity gas puff flow rates.");

        group.MapGet("/status/{divertorId}", (string divertorId) =>
        {
            var nominal = new DivertorTargetTelemetry(divertorId);
            return Results.Ok(new
            {
                DivertorId = divertorId,
                HeatFluxMegaWattsPerM2 = nominal.TargetHeatFluxMegaWattsPerM2,
                ElectronTemperatureEv = nominal.TargetElectronTemperatureElectronVolts,
                ImpurityGas = nominal.SeededImpurityGas,
                Status = "DETACHED_EXHAUST_NOMINAL"
            });
        })
        .WithName("GetDivertorTargetStatus")
        .WithSummary("Returns tungsten target plate temperature and heat flux telemetry for a tokamak divertor.");

        return app;
    }
}
