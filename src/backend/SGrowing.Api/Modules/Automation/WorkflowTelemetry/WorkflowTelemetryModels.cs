namespace SGrowing.Api.Modules.Automation.WorkflowTelemetry;

public enum WorkflowNodeType
{
    Trigger,
    Filter,
    BranchCondition,
    ActionEmail,
    ActionSms,
    ActionWebhook,
    Delay,
    AiDecisionNode,
    GoalAchieved,
    Terminate
}

public record WorkflowNode(
    string NodeId,
    string Name,
    WorkflowNodeType NodeType,
    Dictionary<string, string>? Configuration = null
);

public record WorkflowEdge(
    string SourceNodeId,
    string TargetNodeId,
    string? BranchConditionValue = null
);

public record WorkflowDefinition(
    string WorkflowId,
    string Name,
    string TriggerNodeId,
    List<WorkflowNode> Nodes,
    List<WorkflowEdge> Edges
);

public record DagValidationResult(
    bool IsValid,
    bool HasCycles,
    List<string> CyclePath,
    List<string> UnreachableNodeIds,
    List<string> ValidationErrors
);

public record SimulationContactContext(
    string ContactId,
    string Email,
    int LeadScore,
    string Country,
    bool HasPurchased,
    decimal LifetimeValue,
    Dictionary<string, string>? CustomAttributes = null
);

public record WorkflowSimulationRequest(
    string TenantId,
    WorkflowDefinition Workflow,
    SimulationContactContext Contact,
    int MaxSteps = 50
);

public record NodeExecutionSnapshot(
    int StepNumber,
    string NodeId,
    string NodeName,
    WorkflowNodeType NodeType,
    string Status,
    string OutputSummary,
    TimeSpan Latency,
    DateTime ExecutedAtUtc
);

public record WorkflowSimulationResult(
    bool CompletedSuccessfully,
    string ExecutionId,
    string WorkflowId,
    string ContactId,
    int TotalStepsExecuted,
    TimeSpan TotalSimulatedDuration,
    List<NodeExecutionSnapshot> ExecutionPath,
    List<string> ActionsDispatched,
    string FinalStatus,
    DateTime SimulatedAtUtc
);

public record WorkflowTelemetryRecord(
    string ExecutionId,
    string TenantId,
    string WorkflowId,
    string ContactId,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    string Status,
    List<NodeExecutionSnapshot> Steps
);
