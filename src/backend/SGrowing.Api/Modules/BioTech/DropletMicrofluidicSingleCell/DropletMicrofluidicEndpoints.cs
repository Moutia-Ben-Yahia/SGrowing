using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.DropletMicrofluidicSingleCell;

public static class DropletMicrofluidicEndpoints
{
    public static IServiceCollection AddDropletMicrofluidicServices(this IServiceCollection services)
    {
        services.AddScoped<IDropletMicrofluidicEngine, DropletMicrofluidicEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDropletMicrofluidicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/droplet-microfluidic-single-cell")
            .WithTags("BioTech Single-Cell Droplet Microfluidics & Poisson Encapsulation");

        group.MapPost("/analyze", (DropletMicrofluidicTelemetry telemetry, IDropletMicrofluidicEngine engine) =>
        {
            var evaluation = engine.AnalyzeEmulsion(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeDropletMicrofluidic")
        .WithSummary("Analyzes droplet microfluidics, Poisson cell/bead loading statistics, emulsion stability, and singlet library purity");

        return endpoints;
    }
}
