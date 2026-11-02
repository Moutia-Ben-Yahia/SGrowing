using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CytidineBaseEditingBystanderMutagenesis;

public static class CytidineBaseEditingEndpoints
{
    public static IServiceCollection AddCytidineBaseEditingBystanderServices(this IServiceCollection services)
    {
        services.AddScoped<ICytidineBaseEditingBystanderMutagenesisEngine, CytidineBaseEditingBystanderMutagenesisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCytidineBaseEditingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/cytidine-base-editing-bystander")
            .WithTags("BioTech Cytidine Base Editing Bystander Mutagenesis");

        group.MapPost("/analyze", (CytidineBaseEditingTelemetry telemetry, ICytidineBaseEditingBystanderMutagenesisEngine engine) =>
        {
            var result = engine.AnalyzeBaseEditing(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCytidineBaseEditingBystander")
        .WithSummary("Analyzes cytidine base editor (CBE) on-target C-to-T conversion, bystander deamination in the R-loop window, and UGI-mediated indel suppression.");

        return endpoints;
    }
}
