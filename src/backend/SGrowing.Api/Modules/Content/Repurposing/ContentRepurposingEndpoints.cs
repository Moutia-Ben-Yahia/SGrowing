using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Content.Repurposing;

public static class ContentRepurposingEndpoints
{
    public static IServiceCollection AddContentRepurposingServices(this IServiceCollection services)
    {
        services.AddSingleton<IContentRepurposingEngine, ContentRepurposingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapContentRepurposingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/content/repurposing")
            .WithTags("Content - Omnichannel Repurposing Mesh");

        group.MapPost("/decompose", (DecomposeContentRequest request, IContentRepurposingEngine engine) =>
        {
            var manifest = engine.DecomposeContent(request);
            return Results.Ok(manifest);
        })
        .WithName("DecomposeCornerstoneContent")
        .WithSummary("Decomposes cornerstone long-form asset into multichannel derivatives");

        group.MapGet("/manifest/{sourceAssetId}", (string sourceAssetId, IContentRepurposingEngine engine) =>
        {
            var manifest = engine.GetManifestByAssetId(sourceAssetId);
            return manifest is not null ? Results.Ok(manifest) : Results.NotFound();
        })
        .WithName("GetRepurposingManifestByAssetId")
        .WithSummary("Retrieves all synthesized derivatives and slides for an asset");

        group.MapGet("/derivative/{derivativeId}", (string derivativeId, IContentRepurposingEngine engine) =>
        {
            var derivative = engine.GetDerivativeById(derivativeId);
            return derivative is not null ? Results.Ok(derivative) : Results.NotFound();
        })
        .WithName("GetDerivativeContentById")
        .WithSummary("Retrieves single channel derivative with slide or body details");

        return endpoints;
    }
}
