using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SEO.TopicalAuthority;

public static class TopicalAuthorityEndpoints
{
    public static IServiceCollection AddTopicalAuthorityServices(this IServiceCollection services)
    {
        services.AddSingleton<ITopicalAuthorityEngine, TopicalAuthorityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTopicalAuthorityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/seo/topical-authority")
            .WithTags("SEO - Dynamic Topical Authority & Internal Link Mesh");

        group.MapPost("/analyze-cluster", (AnalyzeTopicalMeshRequest request, ITopicalAuthorityEngine engine) =>
        {
            var result = engine.AnalyzeTopicalMesh(request);
            return Results.Ok(result);
        })
        .WithName("AnalyzeTopicalMesh")
        .WithSummary("Analyzes website topical entity coverage, clusters, and calculates authority scores");

        group.MapGet("/internal-link-opportunities", (ITopicalAuthorityEngine engine) =>
        {
            var opps = engine.GetInternalLinkOpportunities();
            return Results.Ok(opps);
        })
        .WithName("GetInternalLinkOpportunities")
        .WithSummary("Retrieves prioritized internal link injection recommendations based on cosine semantic relevance");

        group.MapPost("/apply-link-injection", (ApplyLinkInjectionRequest request, ITopicalAuthorityEngine engine) =>
        {
            var result = engine.ApplyLinkInjection(request);
            return Results.Ok(result);
        })
        .WithName("ApplyLinkInjection")
        .WithSummary("Injects contextual internal anchor link into source article HTML");

        return endpoints;
    }
}
