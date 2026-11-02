using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.BioTech.CarTCellExhaustionKinetics;

public static class CarTExhaustionEndpoints
{
    public static IServiceCollection AddCarTCellExhaustionServices(this IServiceCollection services)
    {
        services.AddScoped<ICarTCellExhaustionEngine, CarTCellExhaustionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCarTExhaustionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/cart-exhaustion")
            .WithTags("CAR-T Cell Exhaustion & Epigenetic LAG-3/TIM-3 Kinetics");

        group.MapPost("/evaluate", (
            [FromBody] CarTExhaustionEvaluationRequest request,
            [FromServices] ICarTCellExhaustionEngine engine) =>
        {
            var assessment = engine.EvaluateExhaustion(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateCarTCellExhaustion")
        .WithSummary("Calculates exhaustion hazard index, in-vivo persistence half-life, and IL-7/IL-15 cytokine intervention directives.");

        group.MapGet("/batch/{batchId}", (string batchId) =>
        {
            var nominal = new CarTCellPhenotypeTelemetry(batchId);
            return Results.Ok(new
            {
                BatchId = batchId,
                TargetAntigen = nominal.TargetAntigen,
                StemCellMemoryPct = nominal.StemCellMemoryFraction * 100.0,
                DaysPostInfusion = nominal.DaysPostInfusion,
                Status = "CLONAL_EXPANSION_MONITORED"
            });
        })
        .WithName("GetCarTCellBatchProfile")
        .WithSummary("Returns target antigen, stem-cell memory fraction, and post-infusion day for a CAR-T therapeutic batch.");

        return app;
    }
}
