using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Automation.WorkflowTelemetry;

public static class WorkflowTelemetryEndpoints
{
    public static IServiceCollection AddWorkflowTelemetryServices(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowTelemetryEngine, WorkflowTelemetryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWorkflowTelemetryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/automation/workflows").WithTags("Marketing Automation Visual Workflow Telemetry");

        group.MapPost("/validate-dag", (WorkflowDefinition workflow, IWorkflowTelemetryEngine engine) =>
        {
            var result = engine.ValidateDag(workflow);
            return Results.Ok(result);
        }).WithName("ValidateWorkflowDag");

        group.MapPost("/simulate", (WorkflowSimulationRequest request, IWorkflowTelemetryEngine engine) =>
        {
            var result = engine.SimulateWorkflow(request);
            return Results.Ok(result);
        }).WithName("SimulateWorkflowExecution");

        group.MapGet("/telemetry/{tenantId}/{executionId}", (string tenantId, string executionId, IWorkflowTelemetryEngine engine) =>
        {
            var record = engine.GetTelemetry(tenantId, executionId);
            return record is not null ? Results.Ok(record) : Results.NotFound();
        }).WithName("GetWorkflowExecutionTelemetry");

        return endpoints;
    }
}
