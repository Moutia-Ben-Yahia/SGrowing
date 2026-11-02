namespace SGrowing.Api.Modules.Automation.ExecutionEngine;

public enum WorkflowNodeKind
{
    WebhookTrigger,
    FilterCondition,
    TransformPayload,
    DelayTimer,
    HttpDispatch,
    CrmAction,
    SendEmail
}

public enum NodeExecutionStatus
{
    Pending,
    Running,
    Completed,
    Retrying,
    Failed,
    Skipped
}

public record WorkflowStepDefinition(
    string StepId,
    string Name,
    WorkflowNodeKind Kind,
    Dictionary<string, string> Parameters,
    List<string> NextStepIds
);

public record WorkflowDefinition(
    string WorkflowId,
    string TenantId,
    string Name,
    string Description,
    bool IsActive,
    List<WorkflowStepDefinition> Steps,
    DateTime CreatedAtUtc
);

public record NodeExecutionTrace(
    string StepId,
    string StepName,
    WorkflowNodeKind Kind,
    NodeExecutionStatus Status,
    int AttemptCount,
    string InputJson,
    string OutputJson,
    string? ErrorMessage,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc
);

public record WorkflowExecutionInstance(
    string ExecutionId,
    string WorkflowId,
    string TenantId,
    NodeExecutionStatus Status,
    List<NodeExecutionTrace> Steps,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc
);

public record TriggerWorkflowRequest(
    string TenantId,
    string WorkflowId,
    Dictionary<string, object> InitialPayload
);

public record ReplayStepRequest(
    string TenantId,
    string ExecutionId,
    string StepId
);
