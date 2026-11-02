using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Automation.ExecutionEngine;

public static class WorkflowExecutionEndpoints
{
    public static IServiceCollection AddWorkflowExecutionServices(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWorkflowExecutionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/automation/workflows")
            .WithTags("Domain E1, E2 & E3: Stateful Event Mesh & DAG Workflow Execution Engine");

        // 1. Register Workflow
        group.MapPost("/", (
            [FromBody] WorkflowDefinition definition,
            IWorkflowExecutionEngine engine) =>
        {
            var created = engine.RegisterWorkflow(definition);
            return Results.Created($"/api/v1/automation/workflows/{created.TenantId}/{created.WorkflowId}", created);
        })
        .WithName("RegisterWorkflowDefinition")
        .WithSummary("Registers a DAG workflow execution graph with typed step nodes.");

        // 2. Get Workflow
        group.MapGet("/{tenantId}/{workflowId}", (
            string tenantId,
            string workflowId,
            IWorkflowExecutionEngine engine) =>
        {
            var wf = engine.GetWorkflow(tenantId, workflowId);
            return wf is not null ? Results.Ok(wf) : Results.NotFound();
        })
        .WithName("GetWorkflowDefinition")
        .WithSummary("Retrieves a workflow DAG definition.");

        // 3. Trigger Workflow
        group.MapPost("/trigger", (
            [FromBody] TriggerWorkflowRequest request,
            IWorkflowExecutionEngine engine) =>
        {
            try
            {
                var instance = engine.TriggerWorkflow(request);
                return Results.Ok(instance);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("TriggerWorkflowExecution")
        .WithSummary("Executes a workflow instance step-by-step with state persistence.");

        // 4. Get Execution Instance
        group.MapGet("/executions/{tenantId}/{executionId}", (
            string tenantId,
            string executionId,
            IWorkflowExecutionEngine engine) =>
        {
            var execution = engine.GetExecution(tenantId, executionId);
            return execution is not null ? Results.Ok(execution) : Results.NotFound();
        })
        .WithName("GetWorkflowExecutionTrace")
        .WithSummary("Retrieves detailed step-by-step execution traces and payloads.");

        // 5. Replay Step
        group.MapPost("/executions/replay-step", (
            [FromBody] ReplayStepRequest request,
            IWorkflowExecutionEngine engine) =>
        {
            try
            {
                var replayed = engine.ReplayStep(request);
                return Results.Ok(replayed);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ReplayWorkflowFailedStep")
        .WithSummary("Replays a specific step in an execution trace without re-running earlier steps.");

        return endpoints;
    }
}
