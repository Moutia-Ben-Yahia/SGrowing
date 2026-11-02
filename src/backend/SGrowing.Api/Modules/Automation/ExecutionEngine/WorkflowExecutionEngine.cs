using System.Collections.Concurrent;
using System.Text.Json;

namespace SGrowing.Api.Modules.Automation.ExecutionEngine;

public interface IWorkflowExecutionEngine
{
    WorkflowDefinition RegisterWorkflow(WorkflowDefinition definition);
    WorkflowDefinition? GetWorkflow(string tenantId, string workflowId);
    WorkflowExecutionInstance TriggerWorkflow(TriggerWorkflowRequest request);
    WorkflowExecutionInstance? GetExecution(string tenantId, string executionId);
    List<WorkflowExecutionInstance> ListExecutions(string tenantId, string? workflowId = null);
    WorkflowExecutionInstance ReplayStep(ReplayStepRequest request);
}

public class WorkflowExecutionEngine : IWorkflowExecutionEngine
{
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _workflows = new();
    private readonly ConcurrentDictionary<string, WorkflowExecutionInstance> _executions = new();

    private static string BuildWorkflowKey(string tenantId, string workflowId) => $"{tenantId}::wf::{workflowId}";
    private static string BuildExecutionKey(string tenantId, string executionId) => $"{tenantId}::exec::{executionId}";

    public WorkflowDefinition RegisterWorkflow(WorkflowDefinition definition)
    {
        var key = BuildWorkflowKey(definition.TenantId, definition.WorkflowId);
        _workflows[key] = definition;
        return definition;
    }

    public WorkflowDefinition? GetWorkflow(string tenantId, string workflowId)
    {
        _workflows.TryGetValue(BuildWorkflowKey(tenantId, workflowId), out var wf);
        return wf;
    }

    public WorkflowExecutionInstance TriggerWorkflow(TriggerWorkflowRequest request)
    {
        var wf = GetWorkflow(request.TenantId, request.WorkflowId)
            ?? throw new KeyNotFoundException($"Workflow '{request.WorkflowId}' not found for tenant '{request.TenantId}'.");

        var executionId = "exec_" + Guid.NewGuid().ToString("N")[..10];
        var now = DateTime.UtcNow;

        var traces = new List<NodeExecutionTrace>();
        var currentPayload = new Dictionary<string, object>(request.InitialPayload);
        var executionFailed = false;

        // Traverse steps in topological/sequential order
        foreach (var step in wf.Steps)
        {
            var stepStart = DateTime.UtcNow;
            var inputStr = JsonSerializer.Serialize(currentPayload);

            try
            {
                var outputPayload = ExecuteStepNode(step, currentPayload);
                currentPayload = outputPayload;

                traces.Add(new NodeExecutionTrace(
                    StepId: step.StepId,
                    StepName: step.Name,
                    Kind: step.Kind,
                    Status: NodeExecutionStatus.Completed,
                    AttemptCount: 1,
                    InputJson: inputStr,
                    OutputJson: JsonSerializer.Serialize(outputPayload),
                    ErrorMessage: null,
                    StartedAtUtc: stepStart,
                    FinishedAtUtc: DateTime.UtcNow
                ));
            }
            catch (Exception ex)
            {
                executionFailed = true;
                traces.Add(new NodeExecutionTrace(
                    StepId: step.StepId,
                    StepName: step.Name,
                    Kind: step.Kind,
                    Status: NodeExecutionStatus.Failed,
                    AttemptCount: 1,
                    InputJson: inputStr,
                    OutputJson: "{}",
                    ErrorMessage: ex.Message,
                    StartedAtUtc: stepStart,
                    FinishedAtUtc: DateTime.UtcNow
                ));
                break;
            }
        }

        var instance = new WorkflowExecutionInstance(
            ExecutionId: executionId,
            WorkflowId: wf.WorkflowId,
            TenantId: request.TenantId,
            Status: executionFailed ? NodeExecutionStatus.Failed : NodeExecutionStatus.Completed,
            Steps: traces,
            StartedAtUtc: now,
            CompletedAtUtc: DateTime.UtcNow
        );

        _executions[BuildExecutionKey(request.TenantId, executionId)] = instance;
        return instance;
    }

    public WorkflowExecutionInstance? GetExecution(string tenantId, string executionId)
    {
        _executions.TryGetValue(BuildExecutionKey(tenantId, executionId), out var exec);
        return exec;
    }

    public List<WorkflowExecutionInstance> ListExecutions(string tenantId, string? workflowId = null)
    {
        return _executions.Values
            .Where(e => e.TenantId == tenantId && (workflowId == null || e.WorkflowId == workflowId))
            .OrderByDescending(e => e.StartedAtUtc)
            .ToList();
    }

    public WorkflowExecutionInstance ReplayStep(ReplayStepRequest request)
    {
        var key = BuildExecutionKey(request.TenantId, request.ExecutionId);
        if (!_executions.TryGetValue(key, out var instance))
        {
            throw new KeyNotFoundException($"Execution '{request.ExecutionId}' not found.");
        }

        var stepIdx = instance.Steps.FindIndex(s => s.StepId == request.StepId);
        if (stepIdx < 0)
        {
            throw new KeyNotFoundException($"Step '{request.StepId}' not found in execution trace.");
        }

        var targetTrace = instance.Steps[stepIdx];
        var replayedTrace = targetTrace with
        {
            Status = NodeExecutionStatus.Completed,
            AttemptCount = targetTrace.AttemptCount + 1,
            ErrorMessage = null,
            FinishedAtUtc = DateTime.UtcNow
        };

        var updatedSteps = new List<NodeExecutionTrace>(instance.Steps);
        updatedSteps[stepIdx] = replayedTrace;

        var allSucceeded = updatedSteps.All(s => s.Status == NodeExecutionStatus.Completed);
        var updatedInstance = instance with
        {
            Status = allSucceeded ? NodeExecutionStatus.Completed : NodeExecutionStatus.Failed,
            Steps = updatedSteps,
            CompletedAtUtc = DateTime.UtcNow
        };

        _executions[key] = updatedInstance;
        return updatedInstance;
    }

    private static Dictionary<string, object> ExecuteStepNode(WorkflowStepDefinition step, Dictionary<string, object> input)
    {
        var output = new Dictionary<string, object>(input);

        switch (step.Kind)
        {
            case WorkflowNodeKind.WebhookTrigger:
                output["trigger_received"] = true;
                output["timestamp"] = DateTime.UtcNow.ToString("o");
                break;

            case WorkflowNodeKind.FilterCondition:
                if (step.Parameters.TryGetValue("minScore", out var minScoreStr) &&
                    double.TryParse(minScoreStr, out var minScore))
                {
                    if (input.TryGetValue("leadScore", out var scoreObj) &&
                        double.TryParse(scoreObj?.ToString(), out var score))
                    {
                        if (score < minScore)
                        {
                            throw new InvalidOperationException($"Filter failed: Lead score {score} is below threshold {minScore}.");
                        }
                    }
                }
                output["filter_passed"] = true;
                break;

            case WorkflowNodeKind.TransformPayload:
                output["transformed"] = true;
                output["enriched_tier"] = "Enterprise Tier A";
                break;

            case WorkflowNodeKind.DelayTimer:
                output["delay_completed"] = true;
                break;

            case WorkflowNodeKind.HttpDispatch:
                output["http_status"] = 200;
                output["dispatched_endpoint"] = step.Parameters.GetValueOrDefault("url", "https://api.partner.io/events");
                break;

            case WorkflowNodeKind.CrmAction:
                output["crm_updated"] = true;
                output["contact_status"] = "Qualified";
                break;

            case WorkflowNodeKind.SendEmail:
                output["email_sent"] = true;
                output["template"] = step.Parameters.GetValueOrDefault("template", "welcome_enterprise");
                break;
        }

        return output;
    }
}
