using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Energy.DmfcRutheniumPoisoning;

public static class DmfcPoisoningEndpoints
{
    public static IServiceCollection AddDmfcRutheniumPoisoningServices(this IServiceCollection services)
    {
        services.AddScoped<IDmfcPoisoningEngine, DmfcPoisoningEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDmfcPoisoningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/dmfc-poisoning")
            .WithTags("Direct Methanol Fuel Cell Anode Poisoning & Crossover");

        group.MapPost("/evaluate", (
            [FromBody] DmfcEvaluationRequest request,
            [FromServices] IDmfcPoisoningEngine engine) =>
        {
            var plan = engine.EvaluateCellHealth(request);
            return Results.Ok(plan);
        })
        .WithName("EvaluateDmfcPoisoning")
        .WithSummary("Calculates CO catalyst coverage, methanol crossover efficiency loss, power density, and electrochemical stripping pulses.");

        group.MapGet("/stack/{stackId}", (string stackId) =>
        {
            var nominal = new DmfcCellTelemetry(stackId);
            return Results.Ok(new
            {
                StackId = stackId,
                MethanolMolar = nominal.MethanolFeedConcentrationMolar,
                CellVoltage = nominal.CellOperatingVoltageVolts,
                CurrentDensity = nominal.CurrentDensityMilliAmpsPerCm2,
                Status = "PT_RU_ANODE_ACTIVE"
            });
        })
        .WithName("GetDmfcStackProfile")
        .WithSummary("Returns methanol molarity, operating voltage, and current density for a direct methanol fuel cell stack.");

        return app;
    }
}
