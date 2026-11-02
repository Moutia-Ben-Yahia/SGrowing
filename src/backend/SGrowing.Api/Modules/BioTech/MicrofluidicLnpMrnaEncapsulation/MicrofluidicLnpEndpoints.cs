using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.MicrofluidicLnpMrnaEncapsulation;

public static class MicrofluidicLnpEndpoints
{
    public static IServiceCollection AddMicrofluidicLnpServices(this IServiceCollection services)
    {
        services.AddScoped<IMicrofluidicLnpMrnaEncapsulationEngine, MicrofluidicLnpMrnaEncapsulationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicrofluidicLnpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/microfluidic-lnp")
            .WithTags("BioTech - Microfluidic LNP mRNA Encapsulation");

        group.MapPost("/analyze-formulation", (
            LnpFormulationTelemetry telemetry,
            IMicrofluidicLnpMrnaEncapsulationEngine engine) =>
        {
            var result = engine.AnalyzeLnpFormulation(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLnpFormulation")
        .WithSummary("Analyzes microfluidic lipid nanoparticle (LNP) mRNA formulation, hydrodynamic diameter, polydispersity index (PDI), and encapsulation efficiency.");

        return app;
    }
}
