namespace SGrowing.Core.Entities;

public enum WorkflowNodeType
{
    Trigger = 1,
    Condition = 2,
    Action = 3
}

public record WorkflowNodeConfig(
    string Id,
    string Name,
    WorkflowNodeType Type,
    string Subtype,
    string ConfigJson = "{}"
);

public record WorkflowEdgeConfig(
    string SourceNodeId,
    string TargetNodeId,
    string ConditionBranch = "default"
);

public record WorkflowStepAuditLog(
    string NodeId,
    string NodeName,
    WorkflowNodeType Type,
    string Status, // Executed, Skipped, Failed
    string OutputSummary,
    int LatencyMs
);

public record WorkflowGraphExecutionResult(
    Guid ExecutionId,
    Guid WorkflowId,
    string Status, // Success, Partial, Failed
    int StepsExecutedCount,
    List<WorkflowStepAuditLog> Steps,
    Dictionary<string, object> FinalContext,
    DateTime CompletedAtUtc
);

public record ExecuteWorkflowGraphRequest(
    Guid? WorkflowId = null,
    Dictionary<string, object>? InitialPayload = null
);
