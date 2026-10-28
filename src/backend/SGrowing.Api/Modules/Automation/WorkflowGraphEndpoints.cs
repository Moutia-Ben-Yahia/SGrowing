using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Automation;

public static class WorkflowGraphEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapWorkflowGraphEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/automation/workflows")
            .WithTags("Visual Node Workflow Engine (n8n/Windmill Convergence)");

        group.MapGet("/active-nodes", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWorkflowGraphExecutorService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var nodes = await service.GetActiveGraphNodesAsync(effectiveTenantId, ct);
            return Results.Ok(nodes);
        })
        .WithName("GetActiveGraphNodes")
        .WithSummary("Retrieve the current active node topology and step configurations");

        group.MapPost("/execute-graph", async (
            [FromBody] ExecuteWorkflowGraphRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWorkflowGraphExecutorService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var executionResult = await service.ExecuteGraphAsync(effectiveTenantId, req, ct);
            return Results.Ok(executionResult);
        })
        .WithName("ExecuteWorkflowGraph")
        .WithSummary("Simulate or execute a full visual workflow pipeline with step audit telemetry");

        group.MapGet("/definitions", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWorkflowGraphExecutorService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var definitions = await service.GetWorkflowDefinitionsAsync(effectiveTenantId, ct);
            return Results.Ok(definitions);
        })
        .WithName("GetWorkflowDefinitions")
        .WithSummary("List all saved persistent workflow definitions for tenant");

        group.MapGet("/definitions/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWorkflowGraphExecutorService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var definition = await service.GetWorkflowDefinitionByIdAsync(effectiveTenantId, id, ct);
            if (definition == null) return Results.NotFound(new { error = $"Workflow definition '{id}' not found" });
            return Results.Ok(definition);
        })
        .WithName("GetWorkflowDefinitionById")
        .WithSummary("Retrieve a specific workflow definition with configured nodes and edges");

        group.MapPost("/definitions", async (
            [FromBody] SaveWorkflowDefinitionRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWorkflowGraphExecutorService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var definition = await service.SaveWorkflowDefinitionAsync(effectiveTenantId, req, null, ct);
            return Results.Created($"/api/v1/automation/workflows/definitions/{definition.Id}", definition);
        })
        .WithName("CreateWorkflowDefinition")
        .WithSummary("Create and save a new persistent visual workflow graph definition");

        group.MapPut("/definitions/{id:guid}", async (
            Guid id,
            [FromBody] SaveWorkflowDefinitionRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWorkflowGraphExecutorService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var definition = await service.SaveWorkflowDefinitionAsync(effectiveTenantId, req, id, ct);
            return Results.Ok(definition);
        })
        .WithName("UpdateWorkflowDefinition")
        .WithSummary("Update an existing persistent workflow graph definition");

        return app;
    }
}
