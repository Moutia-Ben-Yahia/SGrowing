using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.PemElectrolyzerGasCrossover;

public static class PemElectrolyzerGasCrossoverEndpoints
{
    public static IServiceCollection AddPemElectrolyzerGasCrossoverServices(this IServiceCollection services)
    {
        services.AddScoped<IPemElectrolyzerEngine, PemElectrolyzerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPemElectrolyzerGasCrossoverEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/pem-electrolyzer-gas-crossover")
            .WithTags("Energy PEM Electrolyzer Polarization & Gas Crossover");

        group.MapPost("/analyze", (PemElectrolyzerTelemetry telemetry, IPemElectrolyzerEngine engine) =>
        {
            var evaluation = engine.AnalyzeElectrolyzer(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzePemElectrolyzerGasCrossover")
        .WithSummary("Analyzes high-current PEM water electrolyzer electrochemical polarizations, titanium bipolar contact resistance, and hydrogen-in-oxygen crossover flammability margins");

        return endpoints;
    }
}
