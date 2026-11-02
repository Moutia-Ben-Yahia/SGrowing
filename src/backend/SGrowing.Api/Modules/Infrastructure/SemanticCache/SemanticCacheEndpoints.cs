using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.SemanticCache;

public static class SemanticCacheEndpoints
{
    public static IServiceCollection AddSemanticCacheServices(this IServiceCollection services)
    {
        services.AddSingleton<ISemanticCacheEngine, SemanticCacheEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSemanticCacheEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/semantic-cache")
            .WithTags("Infrastructure - Semantic Vector Caching");

        group.MapPost("/query-or-fetch", (SemanticCacheQueryRequest request, ISemanticCacheEngine engine) =>
        {
            var result = engine.QueryOrFetch(request);
            return Results.Ok(result);
        })
        .WithName("QueryOrFetchSemanticCache")
        .WithSummary("Queries edge semantic vector cache; returns cached completion in <5ms on cosine match (>=0.94)");

        group.MapGet("/metrics", (ISemanticCacheEngine engine) =>
        {
            var metrics = engine.GetMetrics();
            return Results.Ok(metrics);
        })
        .WithName("GetSemanticCacheMetrics")
        .WithSummary("Retrieves FinOps metrics, hit rate %, dollars saved, and latency reduction breakdown");

        group.MapPost("/flush", (FlushCacheRequest request, ISemanticCacheEngine engine) =>
        {
            var count = engine.FlushCache(request);
            return Results.Ok(new { FlushedEntriesCount = count });
        })
        .WithName("FlushSemanticCache")
        .WithSummary("Flushes cached vector embeddings and completions from hot memory");

        return endpoints;
    }
}
