namespace SGrowing.Api.Modules.AI.VectorRag;

public static class VectorRagEndpoints
{
    public static IServiceCollection AddVectorRagServices(this IServiceCollection services)
    {
        services.AddSingleton<IVectorRagEngine, VectorRagEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVectorRagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/rag")
            .WithTags("Multi-LLM Semantic Vector RAG Engine");

        group.MapPost("/search", (IVectorRagEngine engine, SemanticSearchQuery query) =>
        {
            var results = engine.SearchSimilar(query);
            return Results.Ok(results);
        })
        .WithName("SearchSimilarChunks")
        .WithSummary("Execute cosine similarity vector search across knowledge base embeddings");

        group.MapPost("/ground", (IVectorRagEngine engine, string query, string? category) =>
        {
            var grounded = engine.GroundPrompt(query, category);
            return Results.Ok(grounded);
        })
        .WithName("GroundPromptWithRag")
        .WithSummary("Synthesize an LLM prompt response grounded in verified semantic document chunks");

        group.MapPost("/ingest", (IVectorRagEngine engine, IngestDocumentRequest request) =>
        {
            var docId = "doc_" + Guid.NewGuid().ToString("N")[..8];
            var chunk = engine.IngestDocumentChunk(docId, request.Title, request.Content, request.Category);
            return Results.Created($"/api/v1/ai/rag/chunks/{chunk.ChunkId}", chunk);
        })
        .WithName("IngestDocumentChunk")
        .WithSummary("Chunk and index a new knowledge base text document into the vector space");

        group.MapGet("/stats", (IVectorRagEngine engine) =>
        {
            return Results.Ok(new
            {
                totalIndexedChunks = engine.GetTotalIndexedChunks(),
                vectorDimensions = 64,
                distanceMetric = "Cosine",
                status = "Healthy"
            });
        })
        .WithName("GetRagIndexStats")
        .WithSummary("Retrieve total vector count, embedding dimensionality, and index health");

        return app;
    }
}
