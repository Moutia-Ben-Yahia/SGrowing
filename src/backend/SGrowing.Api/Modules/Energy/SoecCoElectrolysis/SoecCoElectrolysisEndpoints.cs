namespace SGrowing.Api.Modules.Energy.SoecCoElectrolysis;

public static class SoecCoElectrolysisEndpoints
{
    public static IServiceCollection AddSoecCoElectrolysisServices(this IServiceCollection services)
    {
        services.AddScoped<SoecCoElectrolysisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSoecCoElectrolysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/soec-co-electrolysis")
            .WithTags("SOEC High-Temperature Steam-CO2 Co-Electrolysis Arbiter");

        group.MapPost("/analyze", (SoecCoElectrolysisTelemetry telemetry, SoecCoElectrolysisEngine engine) =>
        {
            var result = engine.AnalyzeStack(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSoecCoElectrolysis")
        .WithSummary("Analyzes high-temperature SOEC steam-CO2 co-electrolysis, thermoneutral voltage regime, and Boudouard carbon coking kinetics.");

        return endpoints;
    }
}
