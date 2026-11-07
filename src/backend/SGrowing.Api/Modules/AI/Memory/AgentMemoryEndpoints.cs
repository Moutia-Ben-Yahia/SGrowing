using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.AI.Memory;

/// <summary>
/// Minimal API endpoints for the multi-tier agent memory engine (PRD Section 6.5 & Domain J1).
/// Implements <see cref="IEndpointModule"/> for parameterless auto-discovery.
/// </summary>
public class AgentMemoryEndpoints : IEndpointModule
{
    private const string DefaultTenantId = AgentMemoryService.DefaultTenantId;

    public AgentMemoryEndpoints()
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai/memory")
            .WithTags("Structured Multi-Tier Agent Memory Engine");

        // 1. POST /api/v1/ai/memory/store
        group.MapPost("/store", (
            [FromBody] StoreMemoryRequest request,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var tenantId = !string.IsNullOrWhiteSpace(request.TenantId)
                ? request.TenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var effectiveRequest = request with { TenantId = tenantId };
            var entry = service.StoreMemory(effectiveRequest);
            return Results.Created($"/api/v1/ai/memory/{entry.Id}", entry);
        })
        .WithName("StoreAgentMemory")
        .WithSummary("Persist a verified agent memory, organizational rule, or campaign constraint");

        // 2. POST /api/v1/ai/memory/query
        group.MapPost("/query", (
            [FromBody] QueryMemoryRequest request,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var tenantId = !string.IsNullOrWhiteSpace(request.TenantId)
                ? request.TenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var effectiveRequest = request with { TenantId = tenantId };
            var results = service.QueryMemory(effectiveRequest);
            return Results.Ok(results);
        })
        .WithName("QueryAgentMemory")
        .WithSummary("Query agent memories with keyword relevance scoring and confidence thresholds");

        // 3. GET /api/v1/ai/memory/category/{category}
        group.MapGet("/category/{category}", (
            string category,
            [FromQuery] string? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            if (!Enum.TryParse<AgentMemoryCategory>(category, true, out var parsedCategory))
            {
                return Results.BadRequest(new { error = $"Invalid category '{category}'. Allowed values: {string.Join(", ", Enum.GetNames<AgentMemoryCategory>())}" });
            }

            var entries = service.GetMemoryByCategory(effectiveTenantId, parsedCategory);
            return Results.Ok(entries);
        })
        .WithName("GetMemoriesByCategory")
        .WithSummary("Retrieve active memories for a given tenant filtered by category");

        // 4. DELETE /api/v1/ai/memory/{id}
        group.MapDelete("/{id}", (
            string id,
            [FromQuery] string? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var deleted = service.DeleteMemory(effectiveTenantId, id);
            return deleted ? Results.NoContent() : Results.NotFound(new { error = $"Memory '{id}' not found for tenant '{effectiveTenantId}'." });
        })
        .WithName("DeleteAgentMemory")
        .WithSummary("Delete an agent memory entry by ID");

        // 5. POST /api/v1/ai/memory/prune
        group.MapPost("/prune", (
            [FromQuery] string? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var prunedCount = service.PruneExpired(effectiveTenantId);
            return Results.Ok(new { prunedCount, tenantId = effectiveTenantId });
        })
        .WithName("PruneExpiredMemories")
        .WithSummary("Prune expired memories whose TTL has elapsed");

        // 6. GET /api/v1/ai/memory/prompt-context
        group.MapGet("/prompt-context", (
            [FromQuery] string? tenantId,
            [FromQuery] string? categories,
            [FromQuery] string? query,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            List<AgentMemoryCategory>? parsedCategories = null;
            if (!string.IsNullOrWhiteSpace(categories))
            {
                parsedCategories = categories
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(c => Enum.TryParse<AgentMemoryCategory>(c, true, out var cat) ? (AgentMemoryCategory?)cat : null)
                    .Where(c => c.HasValue)
                    .Select(c => c!.Value)
                    .ToList();
            }

            var promptContext = service.FormatMemoryForPromptInjection(effectiveTenantId, parsedCategories, query);
            return Results.Ok(new { promptContext, tenantId = effectiveTenantId });
        })
        .WithName("GetMemoryPromptContext")
        .WithSummary("Generate an LLM system prompt injection string from tenant memories");

        // 7. GET /api/v1/ai/memory (List all active memories)
        group.MapGet("", (
            [FromQuery] string? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var entries = service.GetAllMemories(effectiveTenantId);
            return Results.Ok(entries);
        })
        .WithName("GetAllAgentMemories")
        .WithSummary("Retrieve all active memories for a tenant");

        // 8. GET /api/v1/ai/memory/{id}
        group.MapGet("/{id}", (
            string id,
            [FromQuery] string? tenantId,
            [FromHeader(Name = "X-Tenant-Id")] string? headerTenantId,
            IAgentMemoryService service) =>
        {
            var effectiveTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(headerTenantId) ? headerTenantId : DefaultTenantId);

            var entry = service.GetMemoryById(effectiveTenantId, id);
            return entry is not null
                ? Results.Ok(entry)
                : Results.NotFound(new { error = $"Memory '{id}' not found for tenant '{effectiveTenantId}'." });
        })
        .WithName("GetAgentMemoryById")
        .WithSummary("Retrieve a single agent memory entry by ID");
    }
}

public static class AgentMemoryServiceExtensions
{
    public static IServiceCollection AddAgentMemoryServices(this IServiceCollection services)
    {
        services.AddSingleton<IAgentMemoryService, AgentMemoryService>();
        return services;
    }
}
