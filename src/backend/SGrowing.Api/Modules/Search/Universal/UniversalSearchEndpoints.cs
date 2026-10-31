namespace SGrowing.Api.Modules.Search.Universal;

public static class UniversalSearchEndpoints
{
    public static IEndpointRouteBuilder MapUniversalSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search/universal")
            .WithTags("Universal Cross-Entity Search");

        group.MapPost("/query", async (
            UniversalSearchQuery query,
            IUniversalSearchEngineService service,
            CancellationToken ct) =>
        {
            var response = await service.SearchAsync(query, ct);
            return Results.Ok(response);
        })
        .WithName("ExecuteUniversalSearch")
        .WithSummary("Performs BM25 ranked full-text and fuzzy search across Customers, Tickets, Orders, Contracts, Deals, and Knowledge Base.");

        group.MapPost("/index-document", async (
            SearchDocument document,
            IUniversalSearchEngineService service,
            CancellationToken ct) =>
        {
            await service.IndexDocumentAsync(document, ct);
            return Results.Ok(new { success = true, documentId = document.DocumentId });
        })
        .WithName("IndexSearchDocument")
        .WithSummary("Indexes a document into the universal inverted index.");

        group.MapGet("/stats", async (
            Guid tenantId,
            IUniversalSearchEngineService service,
            CancellationToken ct) =>
        {
            int count = await service.GetDocumentCountAsync(tenantId, ct);
            return Results.Ok(new { tenantId, totalIndexedDocuments = count });
        })
        .WithName("GetUniversalSearchStats")
        .WithSummary("Retrieves index statistics for a tenant.");

        return app;
    }
}
