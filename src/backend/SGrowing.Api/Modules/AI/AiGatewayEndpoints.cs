using Microsoft.AspNetCore.Mvc;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI;

public static class AiGatewayEndpoints
{
    public static IEndpointRouteBuilder MapAiGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        // Autonomous AI Agent
        app.MapPost("/api/ai/agent/handle", async ([FromQuery] Guid tenantId, [FromBody] AutonomousAgentQueryDto req, AutonomousAiAgentService agentService) =>
        {
            var res = await agentService.HandleIncomingMessageAsync(tenantId, req.ConversationId, req.ContactId, req.Message);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        // Enterprise AI Gateway & Prompt Firewall
        app.MapPost("/api/ai/firewall/inspect", ([FromBody] InspectPromptDto req, PromptFirewallService firewall) =>
        {
            var result = firewall.InspectAndSanitize(req.UserPrompt);
            return Results.Ok(result);
        });

        app.MapPost("/api/ai/gateway/route", ([FromBody] RouteModelDto req, AiModelRouterService router) =>
        {
            var decision = router.SelectModelForTask(req.Complexity, req.TaskHint);
            return Results.Ok(decision);
        });

        app.MapPost("/api/ai/tools/register", async ([FromBody] RegisterToolDto req, DynamicAgentToolRegistry toolRegistry) =>
        {
            var tool = await toolRegistry.RegisterToolAsync(req.TenantId, req.Name, req.Description, req.SchemaJson, req.TargetUrl, req.HttpMethod ?? "POST");
            return Results.Ok(tool);
        });

        app.MapGet("/api/ai/tools", async ([FromQuery] Guid tenantId, DynamicAgentToolRegistry toolRegistry) =>
        {
            var tools = await toolRegistry.GetActiveToolsAsync(tenantId);
            return Results.Ok(tools);
        });

        app.MapPost("/api/ai/tools/execute", async ([FromBody] ExecuteToolDto req, DynamicAgentToolRegistry toolRegistry) =>
        {
            var result = await toolRegistry.ExecuteToolAsync(req.TenantId, req.ToolName, req.ArgumentsJson);
            return Results.Ok(result);
        });

        // PostgreSQL pgvector RAG & Hybrid Search
        app.MapPost("/api/rag/documents/index", async ([FromBody] IndexDocumentDto req, VectorRagRetrievalService ragService) =>
        {
            var chunks = await ragService.IndexDocumentAsync(req.TenantId, req.DocumentId, req.Title, req.Content, req.SourceUrl);
            return Results.Ok(new { indexedChunks = chunks.Count, documentId = req.DocumentId });
        });

        app.MapPost("/api/rag/search", async ([FromBody] SearchRagDto req, VectorRagRetrievalService ragService) =>
        {
            var results = await ragService.HybridSearchAsync(req.TenantId, req.Query, req.TopK);
            return Results.Ok(results);
        });

        app.MapPost("/api/rag/grounded-context", async ([FromBody] SearchRagDto req, VectorRagRetrievalService ragService) =>
        {
            var context = await ragService.GenerateGroundedContextAsync(req.TenantId, req.Query, req.TopK);
            return Results.Ok(context);
        });

        // Track 3: Vector RAG & Semantic Knowledge Search
        app.MapPost("/api/knowledge/semantic-search", async ([FromBody] SemanticSearchRequestDto req, ISemanticVectorRagService ragService) =>
        {
            var results = await ragService.SearchKnowledgeAsync(req.TenantId, req.Query, req.TopK, req.MinScore);
            return Results.Ok(results);
        });

        app.MapPost("/api/knowledge/index-articles", async ([FromBody] IndexKnowledgeRequestDto req, ISemanticVectorRagService ragService) =>
        {
            var indexedCount = await ragService.IndexTenantKnowledgeAsync(req.TenantId);
            return Results.Ok(new { status = "indexed", count = indexedCount });
        });

        return app;
    }
}
