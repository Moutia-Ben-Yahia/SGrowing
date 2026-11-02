using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Manufacturing.EuvLithographyPellicle;

public static class EuvPellicleEndpoints
{
    public static IServiceCollection AddEuvLithographyPellicleServices(this IServiceCollection services)
    {
        services.AddSingleton<IEuvLithographyPellicleEngine, EuvLithographyPellicleEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEuvLithographyPellicleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/manufacturing/euv-pellicle")
            .WithTags("Manufacturing - EUV Lithography Pellicle Lifecycle");

        group.MapPost("/evaluate-lifecycle", (
            PellicleEvaluationRequest request,
            IEuvLithographyPellicleEngine engine) =>
        {
            var assessment = engine.EvaluatePellicleLifecycle(request);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateEuvPellicleLifecycle")
        .WithSummary("Models extreme ultraviolet thermal loads, hydrogen radical etching, and predicts pellicle replacement schedules for advanced chip fab")
        .Produces<PellicleLifecycleAssessment>(StatusCodes.Status200OK);

        return endpoints;
    }
}
