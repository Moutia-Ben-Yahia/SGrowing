namespace SGrowing.Api.Modules.Agriculture.PrecisionAgrigenomics;

public static class PrecisionAgrigenomicsEndpoints
{
    public static IServiceCollection AddPrecisionAgrigenomicsServices(this IServiceCollection services)
    {
        services.AddSingleton<PrecisionAgrigenomicsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPrecisionAgrigenomicsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agriculture/precision-agrigenomics")
            .WithTags("Precision Agrigenomics");

        group.MapPost("/generate-prescriptions", (AgrigenomicsPrescriptionRequest request, PrecisionAgrigenomicsEngine engine) =>
        {
            var result = engine.GeneratePrescriptions(request);
            return Results.Ok(result);
        })
        .WithName("GenerateAgrigenomicsPrescriptions")
        .WithSummary("Generates variable-rate NPK fertilizer prescription maps and yield phenotyping based on drone multi-spectral vegetative indices");

        return app;
    }
}
