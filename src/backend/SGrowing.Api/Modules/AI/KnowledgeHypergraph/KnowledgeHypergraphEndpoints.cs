using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.AI.KnowledgeHypergraph;

public static class KnowledgeHypergraphEndpoints
{
    public static IServiceCollection AddKnowledgeHypergraphServices(this IServiceCollection services)
    {
        services.AddSingleton<HypergraphKnowledgeEngine>();
        services.AddSingleton<HybridVectorGraphRetriever>();
        services.AddSingleton<ContradictionDetectorService>();
        return services;
    }

    public static IEndpointRouteBuilder MapKnowledgeHypergraphEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/knowledge-hypergraph")
            .WithTags("Enterprise Knowledge Hypergraph & Graph RAG");

        group.MapPost("/query", (
            QueryGraphRagRequest request,
            HybridVectorGraphRetriever retriever) =>
        {
            var result = retriever.Retrieve(request);
            return Results.Ok(result);
        })
        .WithName("QueryHybridGraphRag")
        .WithSummary("Executes hybrid Graph RAG retrieval combining dense embeddings and topological k-hop expansion.");

        group.MapPost("/subgraph", (
            string seedNodeId,
            int? maxHops,
            HypergraphKnowledgeEngine engine) =>
        {
            var subgraph = engine.ExtractSubgraph(seedNodeId, maxHops ?? 2);
            return Results.Ok(subgraph);
        })
        .WithName("ExtractKnowledgeSubgraph")
        .WithSummary("Extracts a k-hop subgraph around a seed entity node with centrality scoring.");

        group.MapGet("/contradictions", (
            ContradictionDetectorService detector) =>
        {
            var contradictions = detector.DetectContradictions();
            return Results.Ok(contradictions);
        })
        .WithName("DetectKnowledgeContradictions")
        .WithSummary("Scans the hypergraph to detect conflicting contract terms, exclusive rights, and jurisdiction clashes.");

        group.MapPost("/nodes", (
            HypergraphNode node,
            HypergraphKnowledgeEngine engine) =>
        {
            engine.AddNode(node);
            return Results.Created($"/api/v1/ai/knowledge-hypergraph/nodes/{node.NodeId}", node);
        })
        .WithName("AddHypergraphNode")
        .WithSummary("Adds a semantic entity node with properties and embedding to the hypergraph.");

        group.MapPost("/edges", (
            HypergraphEdge edge,
            HypergraphKnowledgeEngine engine) =>
        {
            engine.AddEdge(edge);
            return Results.Created($"/api/v1/ai/knowledge-hypergraph/edges/{edge.EdgeId}", edge);
        })
        .WithName("AddHypergraphEdge")
        .WithSummary("Connects two entities with a typed, directional, weighted knowledge relationship.");

        return app;
    }
}
