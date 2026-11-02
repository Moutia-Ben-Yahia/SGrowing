namespace SGrowing.Api.Modules.Energy.HtsRebcoCableDelamination;

public static class HtsRebcoCableEndpoints
{
    public static IServiceCollection AddHtsRebcoCableServices(this IServiceCollection services)
    {
        services.AddScoped<HtsRebcoCableDelaminationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHtsRebcoCableEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/hts-rebco-cable")
            .WithTags("HTS REBCO Cable Delamination & Gradient Arbiter");

        group.MapPost("/analyze", (HtsRebcoCableTelemetry telemetry, HtsRebcoCableDelaminationEngine engine) =>
        {
            var result = engine.AnalyzeCable(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHtsCableDelamination")
        .WithSummary("Analyzes high-field Lorentz peel stress, buffer delamination margin, and tap voltage quench dynamics in REBCO superconducting cables.");

        return endpoints;
    }
}
