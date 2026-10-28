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

public class WorkflowDefinitionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerType { get; set; } = "whatsapp_inbound";
    public string NodesJson { get; set; } = "[]";
    public string EdgesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record SaveWorkflowDefinitionRequest(
    string Name,
    string Description,
    string TriggerType,
    List<WorkflowNodeConfig>? Nodes = null,
    List<WorkflowEdgeConfig>? Edges = null,
    bool IsActive = true
);
