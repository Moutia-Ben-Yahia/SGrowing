namespace SGrowing.Api.Modules.BioTech.CrisprPrimeEditingFlap;

public static class PrimeEditingEndpoints
{
    public static IServiceCollection AddPrimeEditingServices(this IServiceCollection services)
    {
        services.AddScoped<PrimeEditingFlapEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPrimeEditingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/prime-editing")
            .WithTags("CRISPR Prime Editing Flap Resolution Arbiter");

        group.MapPost("/analyze", (PrimeEditingDesignTelemetry telemetry, PrimeEditingFlapEngine engine) =>
        {
            var result = engine.AnalyzeDesign(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzePrimeEditingDesign")
        .WithSummary("Analyzes CRISPR prime editing pegRNA flap hybridization thermodynamics, PE3 secondary nick spacing, and MMR evasion.");

        return endpoints;
    }
}
