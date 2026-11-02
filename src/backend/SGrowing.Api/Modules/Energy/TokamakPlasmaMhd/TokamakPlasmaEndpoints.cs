using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.TokamakPlasmaMhd;

public static class TokamakPlasmaEndpoints
{
    public static IServiceCollection AddTokamakPlasmaMhdServices(this IServiceCollection services)
    {
        services.AddSingleton<ITokamakPlasmaMhdEngine, TokamakPlasmaMhdEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTokamakPlasmaMhdEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/tokamak")
            .WithTags("Energy - Tokamak Plasma MHD Disruption Predictor");

        group.MapPost("/evaluate-disruption-risk", (
            TokamakDisruptionEvaluationRequest request,
            ITokamakPlasmaMhdEngine engine) =>
        {
            var assessment = engine.EvaluateDisruptionRisk(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateTokamakDisruptionRisk")
        .WithSummary("Forecasts tokamak magnetohydrodynamic plasma disruptions, q95 limits, locked modes, and prescribes SPI or ECCD mitigation")
        .Produces<MhdDisruptionAssessment>(StatusCodes.Status200OK);

        return endpoints;
    }
}
