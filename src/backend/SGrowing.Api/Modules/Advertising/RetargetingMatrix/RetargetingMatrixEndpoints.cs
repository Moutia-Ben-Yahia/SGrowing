using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.RetargetingMatrix;

public static class RetargetingMatrixEndpoints
{
    public static IServiceCollection AddRetargetingMatrixServices(this IServiceCollection services)
    {
        services.AddSingleton<IRetargetingMatrixEngine, RetargetingMatrixEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRetargetingMatrixEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/retargeting-matrix")
            .WithTags("Advertising - Retargeting Matrix & Frequency Capping");

        group.MapPost("/evaluate-impression", (EvaluateImpressionRequest request, IRetargetingMatrixEngine engine) =>
        {
            var decision = engine.EvaluateImpression(request);
            return Results.Ok(decision);
        })
        .WithName("EvaluateRetargetingImpression")
        .WithSummary("Evaluates frequency caps and selects stage-specific creative theme");

        group.MapGet("/buyer/{accountDomain}", (string accountDomain, IRetargetingMatrixEngine engine) =>
        {
            var profile = engine.GetProfile(accountDomain);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        })
        .WithName("GetBuyerRetargetingProfile")
        .WithSummary("Retrieves frequency capping status and active retargeting stage");

        group.MapPost("/update-stage", (UpdateBuyerStageRequest request, IRetargetingMatrixEngine engine) =>
        {
            var updated = engine.UpdateStage(request);
            return Results.Ok(updated);
        })
        .WithName("UpdateBuyerRetargetingStage")
        .WithSummary("Transitions buyer to new stage or triggers automatic negative suppression");

        return endpoints;
    }
}
