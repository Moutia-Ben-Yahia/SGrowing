using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Automation;

public record DurableExecuteWorkflowRequest(
    Guid? WorkflowId = null,
    DurableWorkflowDag? Dag = null,
    Dictionary<string, object?>? InitialPayload = null
);

public record ApproveTaskRequest(
    Guid TaskId,
    string ApproverEmail,
    string? Notes = null
);

public record RejectTaskRequest(
    Guid TaskId,
    string ApproverEmail,
    string Reason
);

public record CalculatePacingRequest(
    string? PhoneNumber = null,
    double? Latitude = null,
    double? Longitude = null,
    string? OverrideTimezone = null,
    TimeSpan? QuietStart = null,
    TimeSpan? QuietEnd = null,
    bool ExcludeWeekends = true,
    DateTime? CurrentUtc = null
);

public record WorkflowExecutionDto(
    Guid ExecutionId,
    Guid WorkflowId,
    Guid TenantId,
    string Status,
    string? CurrentNodeId,
    Guid? PendingApprovalTaskId,
    Dictionary<string, object?> Context,
    int EventsCount,
    int ReplayedStepsCount,
    int ExecutedStepsCount,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureReason
);

public static class WorkflowDagReplayEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapWorkflowDagReplayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/automation/workflows")
            .WithTags("Phase 11: Durable Workflow DAG & Temporal State Replay");

        group.MapPost("/durable-execute", async (
            [FromBody] DurableExecuteWorkflowRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDurableWorkflowExecutionEngine engine,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);

            var dag = req.Dag ?? CreateDefaultSampleDag(req.WorkflowId ?? Guid.NewGuid());
            var execution = await engine.StartExecutionAsync(
                effectiveTenantId,
                dag,
                req.InitialPayload,
                null,
                ct
            );

            var dto = MapToDto(execution);
            return Results.Ok(dto);
        })
        .WithName("DurableExecuteWorkflow")
        .WithSummary("Trigger event-sourced durable workflow DAG execution with checkpoint tracking");

        group.MapGet("/{executionId:guid}/history", async (
            Guid executionId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDurableWorkflowExecutionEngine engine,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var history = await engine.GetExecutionHistoryAsync(effectiveTenantId, executionId, ct);
            return Results.Ok(history);
        })
        .WithName("GetWorkflowExecutionHistory")
        .WithSummary("Retrieve the immutable event sourcing audit log for an execution");

        group.MapPost("/hitl/approve", async (
            [FromBody] ApproveTaskRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IHumanInTheLoopApprovalService hitlService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var result = await hitlService.ApproveTaskAsync(
                    effectiveTenantId,
                    req.TaskId,
                    req.ApproverEmail,
                    req.Notes,
                    ct
                );
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ApproveHitlTask")
        .WithSummary("Approve pending human-in-the-loop gate task and resume workflow execution");

        group.MapPost("/hitl/reject", async (
            [FromBody] RejectTaskRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IHumanInTheLoopApprovalService hitlService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var result = await hitlService.RejectTaskAsync(
                    effectiveTenantId,
                    req.TaskId,
                    req.ApproverEmail,
                    req.Reason,
                    ct
                );
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RejectHitlTask")
        .WithSummary("Reject pending human-in-the-loop gate task and cleanly terminate workflow");

        group.MapPost("/timezone/calculate-pacing", (
            [FromBody] CalculatePacingRequest req,
            ILocalTimezonePacingService pacingService) =>
        {
            var utcNow = req.CurrentUtc ?? DateTime.UtcNow;
            var evaluation = pacingService.EvaluatePacing(
                utcNow,
                phoneNumber: req.PhoneNumber,
                latitude: req.Latitude,
                longitude: req.Longitude,
                overrideTimezone: req.OverrideTimezone,
                quietStart: req.QuietStart,
                quietEnd: req.QuietEnd,
                excludeWeekends: req.ExcludeWeekends
            );

            return Results.Ok(evaluation);
        })
        .WithName("CalculateTimezonePacing")
        .WithSummary("Spatially-aware timezone calculator for quiet hours and weekend pacing");

        return app;
    }

    private static DurableWorkflowDag CreateDefaultSampleDag(Guid workflowId)
    {
        var nodes = new List<DurableWorkflowNode>
        {
            new(
                Id: "node-trigger",
                Name: "Inbound Ingestion",
                Type: DurableWorkflowNodeType.TriggerNode,
                Config: new Dictionary<string, object?>
                {
                    ["channel"] = "WhatsApp",
                    ["leadName"] = "Atlas Logistics SARL",
                    ["dealAmount"] = 4500m
                }
            ),
            new(
                Id: "node-ai",
                Name: "AI Lead Qualification",
                Type: DurableWorkflowNodeType.AiPromptNode,
                Config: new Dictionary<string, object?>
                {
                    ["promptTemplate"] = "Assess deal intent for {leadName} with amount {dealAmount}",
                    ["taskType"] = "classify_intent",
                    ["outputVariable"] = "aiQualificationSummary"
                }
            ),
            new(
                Id: "node-condition",
                Name: "High Value Gate Check",
                Type: DurableWorkflowNodeType.ConditionNode,
                Config: new Dictionary<string, object?>
                {
                    ["variable"] = "dealAmount",
                    ["operator"] = ">=",
                    ["value"] = 1000m
                }
            ),
            new(
                Id: "node-hitl",
                Name: "CFO High-Risk Approval Gate",
                Type: DurableWorkflowNodeType.HumanApprovalNode,
                Config: new Dictionary<string, object?>
                {
                    ["description"] = "CFO approval required for transaction {dealAmount} TND from {leadName}",
                    ["financialRiskAmount"] = 4500m
                }
            ),
            new(
                Id: "node-action",
                Name: "Dispatch Enterprise Invoice & CRM Sync",
                Type: DurableWorkflowNodeType.ActionNode,
                Subtype: "generate_invoice",
                Config: new Dictionary<string, object?>
                {
                    ["actionType"] = "generate_invoice",
                    ["currency"] = "TND"
                }
            )
        };

        var edges = new List<DurableWorkflowEdge>
        {
            new("node-trigger", "node-ai"),
            new("node-ai", "node-condition"),
            new("node-condition", "node-hitl", ConditionBranch: "true"),
            new("node-condition", "node-action", ConditionBranch: "false"),
            new("node-hitl", "node-action")
        };

        return new DurableWorkflowDag(workflowId, "Enterprise Order & High-Risk Approval DAG", nodes, edges);
    }

    private static WorkflowExecutionDto MapToDto(WorkflowExecutionInstance instance)
    {
        return new WorkflowExecutionDto(
            ExecutionId: instance.ExecutionId,
            WorkflowId: instance.WorkflowId,
            TenantId: instance.TenantId,
            Status: instance.Status.ToString(),
            CurrentNodeId: instance.CurrentNodeId,
            PendingApprovalTaskId: instance.PendingApprovalTaskId,
            Context: instance.Context,
            EventsCount: instance.History.Count,
            ReplayedStepsCount: instance.ReplayedStepsCount,
            ExecutedStepsCount: instance.ExecutedStepsCount,
            CreatedAtUtc: instance.CreatedAtUtc,
            CompletedAtUtc: instance.CompletedAtUtc,
            FailureReason: instance.FailureReason
        );
    }
}
