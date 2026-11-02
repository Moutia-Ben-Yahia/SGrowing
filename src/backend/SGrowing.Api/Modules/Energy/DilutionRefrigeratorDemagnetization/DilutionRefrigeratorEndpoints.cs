using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.DilutionRefrigeratorDemagnetization;

public static class DilutionRefrigeratorEndpoints
{
    public static IServiceCollection AddDilutionRefrigeratorServices(this IServiceCollection services)
    {
        services.AddScoped<IDilutionRefrigeratorDemagnetizationEngine, DilutionRefrigeratorDemagnetizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDilutionRefrigeratorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/dilution-refrigerator")
            .WithTags("Energy - Sub-Kelvin Dilution Refrigerator ADR");

        group.MapPost("/analyze-cooling", (
            DilutionRefrigeratorTelemetry telemetry,
            IDilutionRefrigeratorDemagnetizationEngine engine) =>
        {
            var result = engine.AnalyzeSubKelvinRefrigeration(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDilutionRefrigeration")
        .WithSummary("Analyzes sub-kelvin He3/He4 dilution refrigeration, adiabatic demagnetization (ADR), and qubit thermal coherence.");

        return app;
    }
}
