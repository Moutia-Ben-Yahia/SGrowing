using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.VideoDco;

public static class VideoDcoEndpoints
{
    public static IServiceCollection AddVideoDcoServices(this IServiceCollection services)
    {
        services.AddSingleton<VideoDcoEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVideoDcoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/video-dco")
            .WithTags("Advertising - Programmatic Video DCO Mesh");

        group.MapGet("/overview", (VideoDcoEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetVideoDcoOverview")
        .WithSummary("Retrieve active video templates, rendered variant throughput, and cache metrics");

        group.MapPost("/generate-manifest", (GenerateVideoManifestRequest request, VideoDcoEngine engine) =>
        {
            var result = engine.GenerateManifest(request);
            return Results.Ok(result);
        })
        .WithName("GenerateVideoDcoManifest")
        .WithSummary("Synthesize dynamic video composition manifest with localized layers");

        group.MapPost("/render-variant", (RenderVideoVariantRequest request, VideoDcoEngine engine) =>
        {
            var receipt = engine.RenderVariant(request);
            return Results.Ok(receipt);
        })
        .WithName("RenderVideoDcoVariant")
        .WithSummary("Dispatch distributed video rendering job with instant CDN edge invalidation");

        return endpoints;
    }
}
