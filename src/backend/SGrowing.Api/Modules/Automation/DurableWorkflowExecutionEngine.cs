using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Automation;

public enum WorkflowExecutionState
{
    Pending = 1,
    Running = 2,
    SuspendedAtHumanGate = 3,
    Completed = 4,
    Failed = 5,
    Terminated = 6
}

public enum DurableWorkflowNodeType
{
    TriggerNode = 1,
    AiPromptNode = 2,
    ConditionNode = 3,
    HumanApprovalNode = 4,
    ActionNode = 5
}

public record WorkflowExecutionEvent(
    Guid EventId,
    Guid ExecutionId,
    string NodeId,
    string EventType,
    string? InputPayloadJson,
    string? OutputPayloadJson,
    DateTime TimestampUtc
);

public record DurableWorkflowNode(
    string Id,
    string Name,
    DurableWorkflowNodeType Type,
    string? Subtype = null,
    Dictionary<string, object?>? Config = null,
    string? NextNodeId = null
);

public record DurableWorkflowEdge(
    string SourceNodeId,
    string TargetNodeId,
    string ConditionBranch = "default"
);

public record DurableWorkflowDag(
    Guid WorkflowId,
    string Name,
    List<DurableWorkflowNode> Nodes,
    List<DurableWorkflowEdge> Edges
);

public class WorkflowExecutionInstance
{
    public Guid ExecutionId { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Guid TenantId { get; set; }
    public WorkflowExecutionState Status { get; set; } = WorkflowExecutionState.Pending;
    public string? CurrentNodeId { get; set; }
    public Guid? PendingApprovalTaskId { get; set; }
    public Dictionary<string, object?> Context { get; set; } = [];
    public List<WorkflowExecutionEvent> History { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public int ReplayedStepsCount { get; set; }
    public int ExecutedStepsCount { get; set; }
    public DurableWorkflowDag? Dag { get; set; }
}

public interface IWorkflowEventStore
{
    Task AppendEventAsync(WorkflowExecutionEvent @event, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowExecutionEvent>> GetEventsAsync(Guid executionId, CancellationToken ct = default);
}

public class InMemoryWorkflowEventStore : IWorkflowEventStore
{
    private readonly ConcurrentDictionary<Guid, List<WorkflowExecutionEvent>> _events = new();

    public Task AppendEventAsync(WorkflowExecutionEvent @event, CancellationToken ct = default)
    {
        _events.AddOrUpdate(
            @event.ExecutionId,
            _ => [@event],
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(@event);
                }
                return list;
            });

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowExecutionEvent>> GetEventsAsync(Guid executionId, CancellationToken ct = default)
    {
        if (_events.TryGetValue(executionId, out var list))
        {
            lock (list)
            {
                return Task.FromResult<IReadOnlyList<WorkflowExecutionEvent>>(list.OrderBy(e => e.TimestampUtc).ToList());
            }
        }

        return Task.FromResult<IReadOnlyList<WorkflowExecutionEvent>>([]);
    }
}

public interface IWorkflowExecutionStore
{
    Task SaveExecutionAsync(WorkflowExecutionInstance execution, CancellationToken ct = default);
    Task<WorkflowExecutionInstance?> GetExecutionAsync(Guid executionId, CancellationToken ct = default);
}

public class InMemoryWorkflowExecutionStore : IWorkflowExecutionStore
{
    private readonly ConcurrentDictionary<Guid, WorkflowExecutionInstance> _executions = new();

    public Task SaveExecutionAsync(WorkflowExecutionInstance execution, CancellationToken ct = default)
    {
        execution.UpdatedAtUtc = DateTime.UtcNow;
        _executions[execution.ExecutionId] = execution;
        return Task.CompletedTask;
    }

    public Task<WorkflowExecutionInstance?> GetExecutionAsync(Guid executionId, CancellationToken ct = default)
    {
        _executions.TryGetValue(executionId, out var instance);
        return Task.FromResult(instance);
    }
}

public interface IWorkflowActionExecutor
{
    Task<Dictionary<string, object?>> ExecuteActionAsync(
        string nodeId,
        string actionType,
        Dictionary<string, object?>? config,
        Dictionary<string, object?> context,
        CancellationToken ct = default);
}

public class DefaultWorkflowActionExecutor : IWorkflowActionExecutor
{
    public Task<Dictionary<string, object?>> ExecuteActionAsync(
        string nodeId,
        string actionType,
        Dictionary<string, object?>? config,
        Dictionary<string, object?> context,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, object?>
        {
            ["actionType"] = actionType,
            ["executedAtUtc"] = DateTime.UtcNow,
            ["status"] = "Success",
            ["nodeId"] = nodeId
        };

        if (config != null)
        {
            foreach (var (k, v) in config)
            {
                result[$"action_cfg_{k}"] = v;
            }
        }

        switch (actionType.ToLowerInvariant())
        {
            case "send_email":
            case "dispatch_whatsapp":
            case "notify_channel":
                result["deliveryStatus"] = "Dispatched";
                result["messageId"] = Guid.NewGuid().ToString("N");
                break;

            case "update_crm":
            case "create_deal":
                result["crmRecordId"] = Guid.NewGuid().ToString("N");
                result["crmStatus"] = "Synced";
                break;

            case "generate_invoice":
                result["invoiceNumber"] = $"INV-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(1000, 9999)}";
                result["invoiceStatus"] = "Issued";
                break;

            default:
                result["output"] = $"Action {actionType} processed successfully.";
                break;
        }

        return Task.FromResult(result);
    }
}

public interface IAiPromptTransformer
{
    Task<string> TransformAsync(
        string promptTemplate,
        Dictionary<string, object?> context,
        string? taskType = null,
        CancellationToken ct = default);
}

public class DefaultAiPromptTransformer : IAiPromptTransformer
{
    public Task<string> TransformAsync(
        string promptTemplate,
        Dictionary<string, object?> context,
        string? taskType = null,
        CancellationToken ct = default)
    {
        var interpolated = promptTemplate;
        foreach (var (key, value) in context)
        {
            interpolated = interpolated.Replace($"{{{key}}}", value?.ToString() ?? string.Empty);
        }

        var result = (taskType?.ToLowerInvariant()) switch
        {
            "classify_intent" =>
                interpolated.Contains("enterprise", StringComparison.OrdinalIgnoreCase) ||
                interpolated.Contains("high", StringComparison.OrdinalIgnoreCase)
                    ? "enterprise_deal_intent"
                    : "general_inquiry_intent",

            "sentiment" =>
                interpolated.Contains("great", StringComparison.OrdinalIgnoreCase) ||
                interpolated.Contains("good", StringComparison.OrdinalIgnoreCase) ||
                interpolated.Contains("positive", StringComparison.OrdinalIgnoreCase)
                    ? "positive"
                    : interpolated.Contains("bad", StringComparison.OrdinalIgnoreCase) ||
                      interpolated.Contains("urgent", StringComparison.OrdinalIgnoreCase)
                        ? "urgent_negative"
                        : "neutral",

            "summarize" =>
                $"[AI Executive Summary]: Processed input successfully. Summary: {interpolated}",

            _ =>
                $"[AI Transformation]: {interpolated}"
        };

        return Task.FromResult(result);
    }
}

public interface IDurableWorkflowExecutionEngine
{
    Task<WorkflowExecutionInstance> StartExecutionAsync(
        Guid tenantId,
        DurableWorkflowDag dag,
        Dictionary<string, object?>? initialPayload = null,
        Guid? executionId = null,
        CancellationToken ct = default);

    Task<WorkflowExecutionInstance> ResumeExecutionAsync(
        Guid tenantId,
        Guid executionId,
        bool isApproved,
        string approverEmail,
        string? notes = null,
        CancellationToken ct = default);

    Task<WorkflowExecutionInstance> TerminateExecutionAsync(
        Guid tenantId,
        Guid executionId,
        string reason,
        CancellationToken ct = default);

    Task<WorkflowExecutionInstance?> GetExecutionAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default);

    Task<IReadOnlyList<WorkflowExecutionEvent>> GetExecutionHistoryAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default);

    Task<WorkflowExecutionInstance> ReplayExecutionAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default);
}

public class DurableWorkflowExecutionEngine : IDurableWorkflowExecutionEngine
{
    private readonly IWorkflowEventStore _eventStore;
    private readonly IWorkflowExecutionStore _executionStore;
    private readonly IWorkflowActionExecutor _actionExecutor;
    private readonly IAiPromptTransformer _aiTransformer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DurableWorkflowExecutionEngine>? _logger;

    public DurableWorkflowExecutionEngine(
        IWorkflowEventStore eventStore,
        IWorkflowExecutionStore executionStore,
        IWorkflowActionExecutor actionExecutor,
        IAiPromptTransformer aiTransformer,
        IServiceProvider serviceProvider,
        ILogger<DurableWorkflowExecutionEngine>? logger = null)
    {
        _eventStore = eventStore;
        _executionStore = executionStore;
        _actionExecutor = actionExecutor;
        _aiTransformer = aiTransformer;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<WorkflowExecutionInstance> StartExecutionAsync(
        Guid tenantId,
        DurableWorkflowDag dag,
        Dictionary<string, object?>? initialPayload = null,
        Guid? executionId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dag);
        if (dag.Nodes == null || dag.Nodes.Count == 0)
        {
            throw new ArgumentException("DAG must contain at least one node.", nameof(dag));
        }

        var execId = executionId ?? Guid.NewGuid();
        var initialContext = initialPayload != null
            ? new Dictionary<string, object?>(initialPayload)
            : [];

        var instance = new WorkflowExecutionInstance
        {
            ExecutionId = execId,
            WorkflowId = dag.WorkflowId,
            TenantId = tenantId,
            Status = WorkflowExecutionState.Pending,
            Context = initialContext,
            Dag = dag,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _executionStore.SaveExecutionAsync(instance, ct);

        // Record ExecutionStarted Event
        var startEvent = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: execId,
            NodeId: "engine",
            EventType: "ExecutionStarted",
            InputPayloadJson: JsonSerializer.Serialize(initialContext),
            OutputPayloadJson: null,
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, startEvent, ct);

        instance.Status = WorkflowExecutionState.Running;
        await _executionStore.SaveExecutionAsync(instance, ct);

        return await RunDagLoopAsync(instance, dag, ct);
    }

    public async Task<WorkflowExecutionInstance> ResumeExecutionAsync(
        Guid tenantId,
        Guid executionId,
        bool isApproved,
        string approverEmail,
        string? notes = null,
        CancellationToken ct = default)
    {
        var instance = await _executionStore.GetExecutionAsync(executionId, ct)
            ?? throw new InvalidOperationException($"Workflow execution '{executionId}' not found.");

        if (instance.TenantId != tenantId && tenantId != Guid.Empty)
        {
            throw new UnauthorizedAccessException($"Tenant mismatch for execution '{executionId}'.");
        }

        if (instance.Status != WorkflowExecutionState.SuspendedAtHumanGate)
        {
            throw new InvalidOperationException($"Cannot resume execution in state '{instance.Status}'. Must be '{WorkflowExecutionState.SuspendedAtHumanGate}'.");
        }

        if (instance.Dag == null)
        {
            throw new InvalidOperationException("Workflow DAG definition is missing from execution instance.");
        }

        if (!isApproved)
        {
            return await TerminateExecutionAsync(tenantId, executionId, notes ?? "Rejected by human decision", ct);
        }

        var approvalGateNodeId = instance.CurrentNodeId ?? "approval_gate";

        // Record HumanApproved Event
        var approvalPayload = new Dictionary<string, object?>
        {
            ["decision"] = "Approved",
            ["approverEmail"] = approverEmail,
            ["notes"] = notes,
            ["approvedAtUtc"] = DateTime.UtcNow
        };

        var approvedEvent = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: executionId,
            NodeId: approvalGateNodeId,
            EventType: "HumanApproved",
            InputPayloadJson: null,
            OutputPayloadJson: JsonSerializer.Serialize(approvalPayload),
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, approvedEvent, ct);

        // Update context with approval data
        instance.Context["approvalDecision"] = "Approved";
        instance.Context["approvedBy"] = approverEmail;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            instance.Context["approvalNotes"] = notes;
        }

        instance.Status = WorkflowExecutionState.Running;
        instance.PendingApprovalTaskId = null;
        await _executionStore.SaveExecutionAsync(instance, ct);

        // Replay historical checkpoints and resume uncompleted nodes
        return await RunDagLoopAsync(instance, instance.Dag, ct);
    }

    public async Task<WorkflowExecutionInstance> TerminateExecutionAsync(
        Guid tenantId,
        Guid executionId,
        string reason,
        CancellationToken ct = default)
    {
        var instance = await _executionStore.GetExecutionAsync(executionId, ct)
            ?? throw new InvalidOperationException($"Workflow execution '{executionId}' not found.");

        if (instance.TenantId != tenantId && tenantId != Guid.Empty)
        {
            throw new UnauthorizedAccessException($"Tenant mismatch for execution '{executionId}'.");
        }

        var rejectionPayload = new Dictionary<string, object?>
        {
            ["decision"] = "Rejected",
            ["reason"] = reason,
            ["rejectedAtUtc"] = DateTime.UtcNow
        };

        var rejectedEvent = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: executionId,
            NodeId: instance.CurrentNodeId ?? "approval_gate",
            EventType: "HumanRejected",
            InputPayloadJson: null,
            OutputPayloadJson: JsonSerializer.Serialize(rejectionPayload),
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, rejectedEvent, ct);

        var termEvent = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: executionId,
            NodeId: "engine",
            EventType: "ExecutionTerminated",
            InputPayloadJson: null,
            OutputPayloadJson: JsonSerializer.Serialize(new { reason }),
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, termEvent, ct);

        instance.Status = WorkflowExecutionState.Terminated;
        instance.FailureReason = reason;
        instance.CompletedAtUtc = DateTime.UtcNow;
        instance.PendingApprovalTaskId = null;

        await _executionStore.SaveExecutionAsync(instance, ct);
        return instance;
    }

    public async Task<WorkflowExecutionInstance?> GetExecutionAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default)
    {
        var instance = await _executionStore.GetExecutionAsync(executionId, ct);
        if (instance != null && instance.TenantId != tenantId && tenantId != Guid.Empty)
        {
            return null;
        }
        return instance;
    }

    public async Task<IReadOnlyList<WorkflowExecutionEvent>> GetExecutionHistoryAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default)
    {
        var instance = await _executionStore.GetExecutionAsync(executionId, ct);
        if (instance != null && instance.TenantId != tenantId && tenantId != Guid.Empty)
        {
            return [];
        }

        return await _eventStore.GetEventsAsync(executionId, ct);
    }

    public async Task<WorkflowExecutionInstance> ReplayExecutionAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken ct = default)
    {
        var instance = await _executionStore.GetExecutionAsync(executionId, ct)
            ?? throw new InvalidOperationException($"Execution '{executionId}' not found.");

        if (instance.TenantId != tenantId && tenantId != Guid.Empty)
        {
            throw new UnauthorizedAccessException($"Tenant mismatch for execution '{executionId}'.");
        }

        if (instance.Dag == null)
        {
            throw new InvalidOperationException("DAG definition is required for replay.");
        }

        return await RunDagLoopAsync(instance, instance.Dag, ct);
    }

    private async Task<WorkflowExecutionInstance> RunDagLoopAsync(
        WorkflowExecutionInstance instance,
        DurableWorkflowDag dag,
        CancellationToken ct)
    {
        // 1. Checkpoint Recovery: Query event history and restore past state
        var events = await _eventStore.GetEventsAsync(instance.ExecutionId, ct);
        var completedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Reconstitute context from completed events
        foreach (var evt in events)
        {
            if (evt.EventType == "ExecutionStarted" && !string.IsNullOrWhiteSpace(evt.InputPayloadJson))
            {
                try
                {
                    var initialInputs = JsonSerializer.Deserialize<Dictionary<string, object?>>(evt.InputPayloadJson);
                    if (initialInputs != null)
                    {
                        foreach (var (k, v) in initialInputs)
                        {
                            instance.Context[k] = UnwrapJsonElement(v);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to deserialize input payload for event {EventId}", evt.EventId);
                }
            }
            else if (evt.EventType is "NodeCompleted" or "HumanApproved")
            {
                completedNodeIds.Add(evt.NodeId);
                if (!string.IsNullOrWhiteSpace(evt.OutputPayloadJson))
                {
                    try
                    {
                        var outputs = JsonSerializer.Deserialize<Dictionary<string, object?>>(evt.OutputPayloadJson);
                        if (outputs != null)
                        {
                            foreach (var (k, v) in outputs)
                            {
                                instance.Context[k] = UnwrapJsonElement(v);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to deserialize output payload for event {EventId}", evt.EventId);
                    }
                }
            }
        }

        // Find starting node (Trigger or first node in DAG)
        var triggerNode = dag.Nodes.FirstOrDefault(n => n.Type == DurableWorkflowNodeType.TriggerNode)
                          ?? dag.Nodes[0];

        var currentNode = triggerNode;
        var visitedInCurrentPass = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (currentNode != null)
        {
            if (!visitedInCurrentPass.Add(currentNode.Id))
            {
                // Prevent infinite cycle in malformed DAG
                _logger?.LogWarning("Cycle detected at node {NodeId} in execution {ExecutionId}", currentNode.Id, instance.ExecutionId);
                break;
            }

            instance.CurrentNodeId = currentNode.Id;

            // CHECKPOINT RECOVERY: If node was already completed, replay from event history WITHOUT repeating side effects!
            if (completedNodeIds.Contains(currentNode.Id))
            {
                instance.ReplayedStepsCount++;
                currentNode = ResolveNextNode(dag, currentNode, instance.Context);
                continue;
            }

            // NEW EXECUTION FROM CHECKPOINT
            try
            {
                var shouldHalt = false;
                switch (currentNode.Type)
                {
                    case DurableWorkflowNodeType.TriggerNode:
                        await ExecuteTriggerNodeAsync(instance, currentNode, ct);
                        break;

                    case DurableWorkflowNodeType.AiPromptNode:
                        await ExecuteAiPromptNodeAsync(instance, currentNode, ct);
                        break;

                    case DurableWorkflowNodeType.ConditionNode:
                        ExecuteConditionNode(instance, currentNode);
                        break;

                    case DurableWorkflowNodeType.HumanApprovalNode:
                        // SUSPEND AT HUMAN GATE
                        await SuspendAtHumanApprovalNodeAsync(instance, currentNode, ct);
                        shouldHalt = true;
                        break;

                    case DurableWorkflowNodeType.ActionNode:
                        await ExecuteActionNodeAsync(instance, currentNode, ct);
                        break;
                }

                if (shouldHalt)
                {
                    // Execution is suspended at human approval node
                    await _executionStore.SaveExecutionAsync(instance, ct);
                    return instance;
                }

                completedNodeIds.Add(currentNode.Id);
                instance.ExecutedStepsCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Node {NodeId} execution failed in execution {ExecutionId}", currentNode.Id, instance.ExecutionId);

                instance.Status = WorkflowExecutionState.Failed;
                instance.FailureReason = ex.Message;
                instance.CompletedAtUtc = DateTime.UtcNow;

                var failedEvent = new WorkflowExecutionEvent(
                    EventId: Guid.NewGuid(),
                    ExecutionId: instance.ExecutionId,
                    NodeId: currentNode.Id,
                    EventType: "ExecutionFailed",
                    InputPayloadJson: null,
                    OutputPayloadJson: JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }),
                    TimestampUtc: DateTime.UtcNow
                );
                await RecordEventAsync(instance, failedEvent, ct);

                await _executionStore.SaveExecutionAsync(instance, ct);
                return instance;
            }

            currentNode = ResolveNextNode(dag, currentNode, instance.Context);
        }

        // All nodes completed
        instance.Status = WorkflowExecutionState.Completed;
        instance.CompletedAtUtc = DateTime.UtcNow;
        instance.CurrentNodeId = null;

        var completedEvent = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: instance.ExecutionId,
            NodeId: "engine",
            EventType: "ExecutionCompleted",
            InputPayloadJson: null,
            OutputPayloadJson: JsonSerializer.Serialize(new { totalContextKeys = instance.Context.Count }),
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, completedEvent, ct);

        await _executionStore.SaveExecutionAsync(instance, ct);
        return instance;
    }

    private async Task ExecuteTriggerNodeAsync(
        WorkflowExecutionInstance instance,
        DurableWorkflowNode node,
        CancellationToken ct)
    {
        var inputJson = JsonSerializer.Serialize(instance.Context);

        if (node.Config != null)
        {
            foreach (var (k, v) in node.Config)
            {
                if (!instance.Context.ContainsKey(k))
                {
                    instance.Context[k] = v;
                }
            }
        }

        var outputJson = JsonSerializer.Serialize(instance.Context);

        var evt = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: instance.ExecutionId,
            NodeId: node.Id,
            EventType: "NodeCompleted",
            InputPayloadJson: inputJson,
            OutputPayloadJson: outputJson,
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, evt, ct);
    }

    private async Task ExecuteAiPromptNodeAsync(
        WorkflowExecutionInstance instance,
        DurableWorkflowNode node,
        CancellationToken ct)
    {
        var inputJson = JsonSerializer.Serialize(instance.Context);

        var promptTemplate = node.Config != null && node.Config.TryGetValue("promptTemplate", out var pt)
            ? pt?.ToString() ?? "Analyze lead"
            : "Analyze context";

        var taskType = node.Config != null && node.Config.TryGetValue("taskType", out var tt)
            ? tt?.ToString()
            : null;

        var outputVar = node.Config != null && node.Config.TryGetValue("outputVariable", out var ov)
            ? ov?.ToString() ?? "aiOutput"
            : "aiOutput";

        var transformation = await _aiTransformer.TransformAsync(promptTemplate, instance.Context, taskType, ct);
        instance.Context[outputVar] = transformation;

        var outputPayload = new Dictionary<string, object?>
        {
            [outputVar] = transformation
        };
        var outputJson = JsonSerializer.Serialize(outputPayload);

        var evt = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: instance.ExecutionId,
            NodeId: node.Id,
            EventType: "NodeCompleted",
            InputPayloadJson: inputJson,
            OutputPayloadJson: outputJson,
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, evt, ct);
    }

    private void ExecuteConditionNode(
        WorkflowExecutionInstance instance,
        DurableWorkflowNode node)
    {
        var inputJson = JsonSerializer.Serialize(instance.Context);
        var evaluationResult = EvaluateConditionExpression(node, instance.Context);

        var outputVar = $"{node.Id}_evaluation";
        instance.Context[outputVar] = evaluationResult;
        instance.Context["_lastConditionResult"] = evaluationResult;

        var outputPayload = new Dictionary<string, object?>
        {
            [outputVar] = evaluationResult,
            ["_lastConditionResult"] = evaluationResult
        };
        var outputJson = JsonSerializer.Serialize(outputPayload);

        var evt = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: instance.ExecutionId,
            NodeId: node.Id,
            EventType: "NodeCompleted",
            InputPayloadJson: inputJson,
            OutputPayloadJson: outputJson,
            TimestampUtc: DateTime.UtcNow
        );
        _ = RecordEventAsync(instance, evt, default);
    }

    private async Task SuspendAtHumanApprovalNodeAsync(
        WorkflowExecutionInstance instance,
        DurableWorkflowNode node,
        CancellationToken ct)
    {
        var description = node.Config != null && node.Config.TryGetValue("description", out var desc)
            ? desc?.ToString() ?? $"Approval required at step '{node.Name}'"
            : $"Approval required at step '{node.Name}'";

        // Interpolate description variables
        foreach (var (k, v) in instance.Context)
        {
            description = description.Replace($"{{{k}}}", v?.ToString() ?? string.Empty);
        }

        decimal? riskAmount = null;
        if (node.Config != null && node.Config.TryGetValue("financialRiskAmount", out var riskObj))
        {
            if (riskObj is decimal d) riskAmount = d;
            else if (decimal.TryParse(riskObj?.ToString(), out var parsed)) riskAmount = parsed;
        }

        if (!riskAmount.HasValue && instance.Context.TryGetValue("financialRiskAmount", out var ctxRisk))
        {
            if (ctxRisk is decimal cd) riskAmount = cd;
            else if (decimal.TryParse(ctxRisk?.ToString(), out var cparsed)) riskAmount = cparsed;
        }

        // Create approval task via IHumanInTheLoopApprovalService
        var hitlService = (IHumanInTheLoopApprovalService)_serviceProvider.GetService(typeof(IHumanInTheLoopApprovalService))!;
        var approvalTask = await hitlService.CreateApprovalTaskAsync(
            instance.TenantId,
            instance.ExecutionId,
            node.Id,
            description,
            riskAmount,
            ct
        );

        instance.Status = WorkflowExecutionState.SuspendedAtHumanGate;
        instance.PendingApprovalTaskId = approvalTask.Id;
        instance.CurrentNodeId = node.Id;

        var suspensionPayload = new Dictionary<string, object?>
        {
            ["approvalTaskId"] = approvalTask.Id,
            ["nodeId"] = node.Id,
            ["description"] = description,
            ["financialRiskAmount"] = riskAmount,
            ["suspendedAtUtc"] = DateTime.UtcNow
        };

        var evt = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: instance.ExecutionId,
            NodeId: node.Id,
            EventType: "ExecutionSuspended",
            InputPayloadJson: JsonSerializer.Serialize(instance.Context),
            OutputPayloadJson: JsonSerializer.Serialize(suspensionPayload),
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, evt, ct);
    }

    private async Task ExecuteActionNodeAsync(
        WorkflowExecutionInstance instance,
        DurableWorkflowNode node,
        CancellationToken ct)
    {
        var inputJson = JsonSerializer.Serialize(instance.Context);

        var actionType = node.Subtype
            ?? (node.Config != null && node.Config.TryGetValue("actionType", out var at) ? at?.ToString() : null)
            ?? "generic_action";

        var actionOutputs = await _actionExecutor.ExecuteActionAsync(
            node.Id,
            actionType,
            node.Config,
            instance.Context,
            ct
        );

        foreach (var (k, v) in actionOutputs)
        {
            instance.Context[k] = v;
        }

        var outputJson = JsonSerializer.Serialize(actionOutputs);

        var evt = new WorkflowExecutionEvent(
            EventId: Guid.NewGuid(),
            ExecutionId: instance.ExecutionId,
            NodeId: node.Id,
            EventType: "NodeCompleted",
            InputPayloadJson: inputJson,
            OutputPayloadJson: outputJson,
            TimestampUtc: DateTime.UtcNow
        );
        await RecordEventAsync(instance, evt, ct);
    }

    private static DurableWorkflowNode? ResolveNextNode(
        DurableWorkflowDag dag,
        DurableWorkflowNode currentNode,
        Dictionary<string, object?> context)
    {
        if (currentNode.Type == DurableWorkflowNodeType.ConditionNode)
        {
            var conditionResult = context.TryGetValue("_lastConditionResult", out var r) && r is bool b && b;
            var branch = conditionResult ? "true" : "false";

            // Find edge matching condition branch
            var branchEdge = dag.Edges.FirstOrDefault(e =>
                e.SourceNodeId.Equals(currentNode.Id, StringComparison.OrdinalIgnoreCase) &&
                e.ConditionBranch.Equals(branch, StringComparison.OrdinalIgnoreCase));

            if (branchEdge != null)
            {
                return dag.Nodes.FirstOrDefault(n => n.Id.Equals(branchEdge.TargetNodeId, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Default edge traversal
        var defaultEdge = dag.Edges.FirstOrDefault(e =>
            e.SourceNodeId.Equals(currentNode.Id, StringComparison.OrdinalIgnoreCase) &&
            (e.ConditionBranch.Equals("default", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(e.ConditionBranch)));

        if (defaultEdge != null)
        {
            return dag.Nodes.FirstOrDefault(n => n.Id.Equals(defaultEdge.TargetNodeId, StringComparison.OrdinalIgnoreCase));
        }

        // Simple NextNodeId property fallback
        if (!string.IsNullOrWhiteSpace(currentNode.NextNodeId))
        {
            return dag.Nodes.FirstOrDefault(n => n.Id.Equals(currentNode.NextNodeId, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static bool EvaluateConditionExpression(
        DurableWorkflowNode node,
        Dictionary<string, object?> context)
    {
        if (node.Config == null) return true;

        var variable = node.Config.TryGetValue("variable", out var v) ? v?.ToString() : null;
        var op = node.Config.TryGetValue("operator", out var o) ? o?.ToString() : "==";
        var targetValue = node.Config.TryGetValue("value", out var tv) ? tv : null;

        if (string.IsNullOrWhiteSpace(variable) || !context.TryGetValue(variable, out var actualValue))
        {
            // If explicit expression string provided
            if (node.Config.TryGetValue("expression", out var exprObj) && exprObj is string expr)
            {
                return EvaluateStringExpression(expr, context);
            }
            return false;
        }

        // Numeric Comparison
        if (TryExtractDecimal(actualValue, out var actDec) && TryExtractDecimal(targetValue, out var tarDec))
        {
            return op switch
            {
                ">" => actDec > tarDec,
                ">=" => actDec >= tarDec,
                "<" => actDec < tarDec,
                "<=" => actDec <= tarDec,
                "==" => actDec == tarDec,
                "!=" => actDec != tarDec,
                _ => false
            };
        }

        // String Comparison
        var actStr = actualValue?.ToString() ?? string.Empty;
        var tarStr = targetValue?.ToString() ?? string.Empty;

        return op switch
        {
            "==" => string.Equals(actStr, tarStr, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(actStr, tarStr, StringComparison.OrdinalIgnoreCase),
            "contains" => actStr.Contains(tarStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool EvaluateStringExpression(string expr, Dictionary<string, object?> context)
    {
        // Simple expression parser e.g. "amount >= 1000"
        var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3)
        {
            var varName = parts[0];
            var op = parts[1];
            var valStr = parts[2].Trim('\'', '"');

            if (context.TryGetValue(varName, out var actVal))
            {
                if (TryExtractDecimal(actVal, out var actDec) && decimal.TryParse(valStr, out var tarDec))
                {
                    return op switch
                    {
                        ">" => actDec > tarDec,
                        ">=" => actDec >= tarDec,
                        "<" => actDec < tarDec,
                        "<=" => actDec <= tarDec,
                        "==" => actDec == tarDec,
                        "!=" => actDec != tarDec,
                        _ => false
                    };
                }

                var actStr = actVal?.ToString() ?? string.Empty;
                return op switch
                {
                    "==" => string.Equals(actStr, valStr, StringComparison.OrdinalIgnoreCase),
                    "!=" => !string.Equals(actStr, valStr, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }
        }

        return true;
    }

    private static bool TryExtractDecimal(object? val, out decimal result)
    {
        if (val is decimal d)
        {
            result = d;
            return true;
        }
        if (val is int i)
        {
            result = i;
            return true;
        }
        if (val is long l)
        {
            result = l;
            return true;
        }
        if (val is double db)
        {
            result = (decimal)db;
            return true;
        }
        if (val is float f)
        {
            result = (decimal)f;
            return true;
        }
        if (val != null && decimal.TryParse(val.ToString(), out var parsed))
        {
            result = parsed;
            return true;
        }

        result = 0;
        return false;
    }

    private static object? UnwrapJsonElement(object? value)
    {
        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number when el.TryGetInt64(out var l) => l,
                JsonValueKind.Number when el.TryGetDecimal(out var d) => d,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => el.ToString()
            };
        }
        return value;
    }

    private async Task RecordEventAsync(
        WorkflowExecutionInstance instance,
        WorkflowExecutionEvent @event,
        CancellationToken ct)
    {
        instance.History.Add(@event);
        await _eventStore.AppendEventAsync(@event, ct);
    }
}
