namespace SGrowing.Api.Modules.BioTech.MicrobialElectrosynthesis;

public static class MicrobialElectrosynthesisEndpoints
{
    public static IServiceCollection AddMicrobialElectrosynthesisServices(this IServiceCollection services)
    {
        services.AddScoped<MicrobialElectrosynthesisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicrobialElectrosynthesisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/microbial-electrosynthesis")
            .WithTags("Microbial Electrosynthesis CO2 Reduction Arbiter");

        group.MapPost("/analyze", (MicrobialElectrosynthesisTelemetry telemetry, MicrobialElectrosynthesisEngine engine) =>
        {
            var result = engine.AnalyzeBiocathode(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMicrobialElectrosynthesis")
        .WithSummary("Analyzes microbial electrosynthesis (MES) CO2 reduction to acetate, Faradaic efficiency, and biofilm cathode overpotential.");

        return endpoints;
    }
}
