using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.AI.Firewall;

public record GenerateCanaryRequest(
    string SessionId,
    string? TenantId = null,
    string? SystemPromptTemplate = null
);

public record GenerateCanaryResponse(
    PromptCanaryToken CanaryToken,
    string? InjectedSystemPrompt
);

public record ScanCanaryRequest(
    string SessionId,
    string OutputText
);

public record SemanticCacheLookupApiRequest(
    Guid TenantId,
    string ToolName,
    float[] QueryEmbedding,
    double SimilarityThreshold = 0.95
);

public record SemanticCacheStoreApiRequest(
    Guid TenantId,
    string ToolName,
    float[] QueryEmbedding,
    string QueryParametersJson,
    string ToolResultJson,
    int OriginalLatencyMs,
    int EstimatedPromptTokens,
    int EstimatedResponseTokens,
    string? EntityId = null
);

public record SanitizePiiRequest(
    string Text
);

public record RouteModelApiRequest(
    string Prompt,
    string? SystemPrompt = null,
    double Temperature = 0.7,
    int MaxTokens = 2048,
    string? SessionId = null
);

public static class PromptFirewallEndpoints
{
    public static IEndpointRouteBuilder MapPromptFirewallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/firewall")
            .WithTags("AI Security Firewall & Multi-Agent Swarm Defense");

        // 1. Generate Cryptographic Prompt Canary
        group.MapPost("/canary/generate", (
            [FromBody] GenerateCanaryRequest req,
            ICryptographicPromptCanaryService canaryService) =>
        {
            if (string.IsNullOrWhiteSpace(req.SessionId))
            {
                return Results.BadRequest(new { error = "SessionId is required." });
            }

            var token = canaryService.GenerateCanary(req.SessionId, req.TenantId);
            string? injectedPrompt = null;
            if (!string.IsNullOrEmpty(req.SystemPromptTemplate))
            {
                injectedPrompt = canaryService.InjectCanary(req.SystemPromptTemplate, req.SessionId);
            }

            return Results.Ok(new GenerateCanaryResponse(token, injectedPrompt));
        })
        .WithName("GeneratePromptCanary")
        .WithSummary("Generate invisible zero-width HMAC canary token and embed into agent prompt");

        // 2. Scan Output for Canary Exfiltration
        group.MapPost("/canary/scan", (
            [FromBody] ScanCanaryRequest req,
            ICryptographicPromptCanaryService canaryService) =>
        {
            if (string.IsNullOrWhiteSpace(req.SessionId))
            {
                return Results.BadRequest(new { error = "SessionId is required." });
            }

            var result = canaryService.ScanResponse(req.SessionId, req.OutputText ?? string.Empty);
            return Results.Ok(result);
        })
        .WithName("ScanOutputCanary")
        .WithSummary("Scan LLM response for canary token exfiltration; locks session on violation");

        // 3. Semantic Vector Tool Cache Lookup
        group.MapPost("/semantic-cache/lookup", async (
            [FromBody] SemanticCacheLookupApiRequest req,
            ISemanticVectorToolCacheService cacheService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ToolName) || req.QueryEmbedding == null || req.QueryEmbedding.Length == 0)
            {
                return Results.BadRequest(new { error = "ToolName and non-empty QueryEmbedding are required." });
            }

            var result = await cacheService.LookupAsync(
                req.TenantId,
                req.ToolName,
                req.QueryEmbedding,
                req.SimilarityThreshold,
                ct);

            return Results.Ok(result);
        })
        .WithName("LookupSemanticToolCache")
        .WithSummary("Query semantic vector cache for previous tool executions with >= 0.95 similarity");

        // 4. Semantic Vector Tool Cache Store
        group.MapPost("/semantic-cache/store", async (
            [FromBody] SemanticCacheStoreApiRequest req,
            ISemanticVectorToolCacheService cacheService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ToolName) || req.QueryEmbedding == null || req.QueryEmbedding.Length == 0)
            {
                return Results.BadRequest(new { error = "ToolName and non-empty QueryEmbedding are required." });
            }

            var entry = await cacheService.StoreAsync(
                tenantId: req.TenantId,
                toolName: req.ToolName,
                queryEmbedding: req.QueryEmbedding,
                queryParametersJson: req.QueryParametersJson ?? "{}",
                toolResultJson: req.ToolResultJson ?? "{}",
                originalLatencyMs: req.OriginalLatencyMs,
                estimatedPromptTokens: req.EstimatedPromptTokens,
                estimatedResponseTokens: req.EstimatedResponseTokens,
                entityId: req.EntityId,
                ct: ct);

            return Results.Ok(entry);
        })
        .WithName("StoreSemanticToolCache")
        .WithSummary("Store embedding, parameters, and output into semantic vector cache");

        // 5. Sanitize PII
        group.MapPost("/sanitize-pii", (
            [FromBody] SanitizePiiRequest req,
            IPiiSanitizationGatewayService piiService) =>
        {
            var result = piiService.Sanitize(req.Text ?? string.Empty);
            return Results.Ok(result);
        })
        .WithName("SanitizePii")
        .WithSummary("Deterministically mask credit cards (Luhn-validated), national IDs (SSN/NIN/NIR), and secrets");

        // 6. Cascading Model Router
        group.MapPost("/route-model", async (
            [FromBody] RouteModelApiRequest req,
            ICascadingModelRouterService routerService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
            {
                return Results.BadRequest(new { error = "Prompt is required." });
            }

            var request = new CascadingRouteRequest(
                Prompt: req.Prompt,
                SystemPrompt: req.SystemPrompt,
                Temperature: req.Temperature,
                MaxTokens: req.MaxTokens,
                SessionId: req.SessionId
            );

            var result = await routerService.ExecuteAsync(request, null, ct);
            return Results.Ok(result);
        })
        .WithName("RouteCascadingModel")
        .WithSummary("Route prompt across cascading priority tiers with circuit breaker and 429/latency failover");

        return app;
    }
}
