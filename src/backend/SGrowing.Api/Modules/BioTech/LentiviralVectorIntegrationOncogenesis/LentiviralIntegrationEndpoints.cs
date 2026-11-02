using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.LentiviralVectorIntegrationOncogenesis;

public static class LentiviralIntegrationEndpoints
{
    public static IServiceCollection AddLentiviralIntegrationServices(this IServiceCollection services)
    {
        services.AddScoped<ILentiviralVectorIntegrationOncogenesisEngine, LentiviralVectorIntegrationOncogenesisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLentiviralIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/lentiviral-integration-oncogenesis")
            .WithTags("BioTech Lentiviral Vector Integration & Oncogenesis");

        group.MapPost("/analyze", (LentiviralIntegrationTelemetry telemetry, ILentiviralVectorIntegrationOncogenesisEngine engine) =>
        {
            var result = engine.AnalyzeIntegrationProfile(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLentiviralIntegration")
        .WithSummary("Analyzes lentiviral vector host genome integration site profiles, Shannon clonality entropy, and insertional oncogenesis hazards.");

        return endpoints;
    }
}
