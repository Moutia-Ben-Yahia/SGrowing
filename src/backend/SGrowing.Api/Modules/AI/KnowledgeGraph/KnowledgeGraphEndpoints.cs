using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Ai.KnowledgeGraph;

public static class KnowledgeGraphEndpoints
{
    public static IServiceCollection AddKnowledgeGraphServices(this IServiceCollection services)
    {
        services.AddSingleton<IKnowledgeGraphEngine, KnowledgeGraphEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapKnowledgeGraphEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/knowledge-graph")
            .WithTags("AI Knowledge Graph");

        group.MapPost("/ingest", (IngestKnowledgeRequest request, IKnowledgeGraphEngine engine) =>
        {
            var result = engine.IngestKnowledge(request);
            return Results.Ok(result);
        });

        group.MapPost("/search", (HybridSearchQuery query, IKnowledgeGraphEngine engine) =>
        {
            var result = engine.HybridSearch(query);
            return Results.Ok(result);
        });

        group.MapGet("/snapshot", (IKnowledgeGraphEngine engine) =>
        {
            var snapshot = engine.GetGraphSnapshot();
            return Results.Ok(snapshot);
        });

        return app;
    }
}
