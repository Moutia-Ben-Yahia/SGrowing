using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Energy.PemElectrolyzerDegradation;

public static class PemElectrolyzerEndpoints
{
    public static IServiceCollection AddPemElectrolyzerDegradationServices(this IServiceCollection services)
    {
        services.AddScoped<IPemElectrolyzerDegradationEngine, PemElectrolyzerDegradationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPemElectrolyzerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/pem-electrolyzer")
            .WithTags("PEM Electrolyzer Degradation & Hydrogen Safety");

        group.MapPost("/evaluate", (
            [FromBody] PemElectrolyzerEvaluationRequest request,
            [FromServices] IPemElectrolyzerDegradationEngine engine) =>
        {
            var assessment = engine.AssessDegradation(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluatePemElectrolyzerDegradation")
        .WithSummary("Assesses membrane thinning, fluorine emission, and hydrogen-in-oxygen crossover hazards for PEM electrolyzers.");

        group.MapGet("/health/{stackId}", (string stackId) =>
        {
            var nominal = new PemElectrolyzerCellTelemetry(stackId);
            return Results.Ok(new
            {
                StackId = stackId,
                CellCount = nominal.CellCount,
                OperatingPressureBar = nominal.OperatingPressureBar,
                TemperatureCelsius = nominal.OperatingTemperatureCelsius,
                Status = "NOMINAL_ELECTROLYSIS_ONLINE"
            });
        })
        .WithName("GetPemElectrolyzerHealth")
        .WithSummary("Returns baseline operational telemetry and cell count for a green hydrogen PEM electrolyzer stack.");

        return app;
    }
}
