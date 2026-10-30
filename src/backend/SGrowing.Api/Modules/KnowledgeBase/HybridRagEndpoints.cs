using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.KnowledgeBase.Autopilot;

namespace SGrowing.Api.Modules.KnowledgeBase;

public record QueryRagEndpointRequest(
    string CustomerQuestion,
    Guid? TenantId = null,
    string? CategoryFilter = null,
    double? MinimumConfidenceThreshold = null,
    int? TopK = null
);

public record EvaluateTicketEndpointRequest(
    string TicketId,
    string CustomerQuestion,
    Guid? TenantId = null,
    double? Threshold = null
);

public record IngestDocumentEndpointRequest(
    string Title,
    string Category,
    string Content,
    List<string>? Tags = null,
    Guid? TenantId = null,
    int? WindowWords = null,
    int? OverlapWords = null
);

public static class HybridRagEndpoints
{
    public static IEndpointRouteBuilder MapHybridRagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/knowledge/rag-autopilot")
            .WithTags("Hybrid RAG & Autopilot Resolution Engine");

        IHybridRagAutopilotService ResolveService(HttpContext ctx, IHybridRagAutopilotService? injected) =>
            injected ?? ctx.RequestServices.GetService<IHybridRagAutopilotService>() ?? HybridRagAutopilotService.DefaultInstance;

        // 1. POST /query: Executes hybrid RAG search and returns grounded answer with citations
        group.MapPost("/query", async (
            [FromBody] QueryRagEndpointRequest req,
            [FromServices] IHybridRagAutopilotService? injectedService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.CustomerQuestion))
            {
                return Results.BadRequest(new { error = "CustomerQuestion is required." });
            }

            var service = ResolveService(ctx, injectedService);
            var queryRequest = new RagQueryRequest(
                TenantId: req.TenantId.GetValueOrDefault(HybridRagAutopilotService.DefaultTenantId),
                CustomerQuestion: req.CustomerQuestion,
                CategoryFilter: req.CategoryFilter,
                MinimumConfidenceThreshold: req.MinimumConfidenceThreshold.GetValueOrDefault(0.88),
                TopK: req.TopK.GetValueOrDefault(5)
            );

            var result = await service.QueryAsync(queryRequest, ct);
            return Results.Ok(result);
        })
        .WithName("ExecuteHybridRagQuery")
        .WithSummary("Executes dense + BM25 + RRF hybrid retrieval and synthesizes grounded answers with citations");

        // 2. POST /evaluate-ticket: Evaluates an incoming support ticket for Autopilot resolution
        group.MapPost("/evaluate-ticket", async (
            [FromBody] EvaluateTicketEndpointRequest req,
            [FromServices] IHybridRagAutopilotService? injectedService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.TicketId) || string.IsNullOrWhiteSpace(req.CustomerQuestion))
            {
                return Results.BadRequest(new { error = "TicketId and CustomerQuestion are required fields." });
            }

            var service = ResolveService(ctx, injectedService);
            var resolution = await service.EvaluateTicketAsync(
                ticketId: req.TicketId,
                customerQuestion: req.CustomerQuestion,
                tenantId: req.TenantId,
                threshold: req.Threshold.GetValueOrDefault(0.88),
                ct: ct
            );

            return Results.Ok(resolution);
        })
        .WithName("EvaluateTicketForAutopilot")
        .WithSummary("Evaluates a support ticket against hybrid RAG; auto-resolves if confidence exceeds threshold");

        // 3. GET /documents: Lists knowledge base documents and chunk stats
        group.MapGet("/documents", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? category,
            [FromServices] IHybridRagAutopilotService? injectedService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var service = ResolveService(ctx, injectedService);
            var documents = await service.GetDocumentsAsync(tenantId, category, ct);
            return Results.Ok(documents);
        })
        .WithName("ListKnowledgeDocuments")
        .WithSummary("Lists indexed knowledge documents, categories, and chunk metadata");

        // 4. POST /documents: Ingests a new document with automated sliding-window chunking
        group.MapPost("/documents", async (
            [FromBody] IngestDocumentEndpointRequest req,
            [FromServices] IHybridRagAutopilotService? injectedService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content) || string.IsNullOrWhiteSpace(req.Category))
            {
                return Results.BadRequest(new { error = "Title, Category, and Content are required fields." });
            }

            var service = ResolveService(ctx, injectedService);
            var doc = await service.IngestDocumentAsync(
                tenantId: req.TenantId.GetValueOrDefault(HybridRagAutopilotService.DefaultTenantId),
                title: req.Title,
                category: req.Category,
                content: req.Content,
                tags: req.Tags,
                windowWords: req.WindowWords.GetValueOrDefault(80),
                overlapWords: req.OverlapWords.GetValueOrDefault(20),
                ct: ct
            );

            return Results.Created($"/api/v1/knowledge/rag-autopilot/documents/{doc.Id}", doc);
        })
        .WithName("IngestKnowledgeDocument")
        .WithSummary("Ingests and partitions a document into dense-indexed and tokenized chunks using sliding windows");

        // 5. GET /autopilot-logs: Audit trail of auto-resolved tickets vs agent-routed queries
        group.MapGet("/autopilot-logs", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] int limit,
            [FromServices] IHybridRagAutopilotService? injectedService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var service = ResolveService(ctx, injectedService);
            int take = limit <= 0 ? 50 : Math.Min(limit, 200);
            var logs = await service.GetAutopilotLogsAsync(tenantId, take, ct);
            return Results.Ok(logs);
        })
        .WithName("GetAutopilotAuditLogs")
        .WithSummary("Retrieves the chronological audit log of tickets evaluated by Autopilot");

        // 6. GET /metrics: Autopilot resolution rate %, average confidence, avg retrieval latency
        group.MapGet("/metrics", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] int? windowHours,
            [FromServices] IHybridRagAutopilotService? injectedService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var service = ResolveService(ctx, injectedService);
            var metrics = await service.GetMetricsAsync(tenantId, ct);
            return Results.Ok(metrics);
        })
        .WithName("GetHybridRagMetrics")
        .WithSummary("Retrieves key performance indicators for Autopilot resolution rate, latency, and corpus size");

        return group;
    }
}
