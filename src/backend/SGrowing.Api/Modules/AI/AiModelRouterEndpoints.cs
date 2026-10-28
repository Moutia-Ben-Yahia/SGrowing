using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI;

public static class AiModelRouterEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapAiModelRouterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/models")
            .WithTags("OpenClaude Multi-Model Router (200+ LLMs)");

        group.MapGet("/catalog", async (
            IAiModelRouterService router,
            CancellationToken ct) =>
        {
            var catalog = await router.GetModelCatalogAsync(ct);
            return Results.Ok(catalog);
        })
        .WithName("GetAiModelCatalog")
        .WithSummary("Query the unified AI model catalog across Google Gemini, OpenAI, Anthropic, OpenRouter, and Ollama");

        group.MapPost("/route", (
            [FromBody] ExecuteRoutedModelRequest req,
            IAiModelRouterService router) =>
        {
            var decision = router.SelectModelForTask(req.Complexity, req.TaskHint ?? req.Prompt);
            return Results.Ok(decision);
        })
        .WithName("RouteAiTaskModel")
        .WithSummary("Determine optimal model and provider based on task complexity and token pricing heuristics");

        group.MapPost("/execute", async (
            [FromBody] ExecuteRoutedModelRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiModelRouterService router,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);

            var execution = await router.ExecuteWithFallbackChainAsync(
                effectiveTenantId,
                req.Complexity,
                (model, provider) => Task.FromResult($"[{model} on {provider}] Synthesized answer for prompt: '{req.Prompt}'"),
                req.TaskHint,
                ct
            );

            return Results.Ok(execution);
        })
        .WithName("ExecuteRoutedAiTaskWithFallback")
        .WithSummary("Execute AI task through the OpenClaude self-healing fallback chain");

        group.MapGet("/quotas", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IAiModelRouterService router,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var quotas = await router.GetTenantTokenQuotaAsync(effectiveTenantId, ct);
            return Results.Ok(quotas);
        })
        .WithName("GetTenantTokenQuotas")
        .WithSummary("Get monthly AI token budget, consumed tokens, and remaining quota");

        return app;
    }
}
