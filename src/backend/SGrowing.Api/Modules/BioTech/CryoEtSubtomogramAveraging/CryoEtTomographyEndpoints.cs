using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CryoEtSubtomogramAveraging;

public static class CryoEtTomographyEndpoints
{
    public static IServiceCollection AddCryoEtTomographyServices(this IServiceCollection services)
    {
        services.AddScoped<ICryoEtTomographyEngine, CryoEtTomographyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCryoEtTomographyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/cryo-et-subtomogram-averaging")
            .WithTags("BioTech Cryo-ET Subtomogram Averaging");

        group.MapPost("/analyze", (CryoEtTomographyTelemetry telemetry, ICryoEtTomographyEngine engine) =>
        {
            var evaluation = engine.AnalyzeTomography(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeCryoEtTomography")
        .WithSummary("Analyzes in-situ cryo-electron tomography tilt series, missing wedge fractions, electron dose fractionation, and subtomogram averaging FSC resolution");

        return endpoints;
    }
}
