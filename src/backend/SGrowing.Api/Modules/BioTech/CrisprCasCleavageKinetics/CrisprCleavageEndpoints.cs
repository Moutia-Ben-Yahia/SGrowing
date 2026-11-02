using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CrisprCasCleavageKinetics;

public static class CrisprCleavageEndpoints
{
    public static IServiceCollection AddCrisprCasCleavageServices(this IServiceCollection services)
    {
        services.AddSingleton<ICrisprCasCleavageKineticsEngine, CrisprCasCleavageKineticsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrisprCasCleavageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/crispr")
            .WithTags("BioTech - CRISPR-Cas12a Off-Target Cleavage Predictor");

        group.MapPost("/evaluate-cleavage", (
            CrisprCleavageEvaluationRequest request,
            ICrisprCasCleavageKineticsEngine engine) =>
        {
            var result = engine.EvaluateOffTargetCleavage(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateCrisprOffTargetCleavage")
        .WithSummary("Models RNA-DNA hybrid Gibbs free energy and predicts genome-wide CRISPR-Cas12a off-target cleavage probability")
        .Produces<CrisprSafetyEvaluationResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
