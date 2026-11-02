using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CrisprRnpElectroporationKinetics;

public static class CrisprRnpElectroporationEndpoints
{
    public static IServiceCollection AddCrisprRnpElectroporationServices(this IServiceCollection services)
    {
        services.AddScoped<ICrisprRnpElectroporationKineticsEngine, CrisprRnpElectroporationKineticsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrisprRnpElectroporationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/crispr-rnp-electroporation")
            .WithTags("BioTech CRISPR RNP Electroporation Delivery Kinetics");

        group.MapPost("/analyze", (CrisprRnpElectroporationTelemetry telemetry, ICrisprRnpElectroporationKineticsEngine engine) =>
        {
            var result = engine.AnalyzeElectroporation(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCrisprRnpElectroporation")
        .WithSummary("Analyzes non-viral CRISPR Cas9/Cas12a RNP electroporation delivery, cell membrane permeabilization, viability, and off-target cleavage suppression.");

        return endpoints;
    }
}
