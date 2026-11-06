namespace SGrowing.Api.Modules.AI.Missions;

/// <summary>
/// High-level marketing and growth objective provided to the autonomous agent swarm.
/// </summary>
public record MissionObjectiveRequest(
    string Title,
    string StrategicGoal,
    decimal BudgetLimit,
    string TargetGeo,
    List<string>? Constraints = null
);

/// <summary>
/// Lifecycle status of an autonomous mission plan.
/// </summary>
public enum MissionStatus
{
    Draft,
    Planning,
    InProgress,
    AwaitingApproval,
    Completed,
    Failed
}

/// <summary>
/// Status of an individual task node in the mission DAG.
/// </summary>
public enum TaskExecutionStatus
{
    Pending,
    Running,
    WaitingApproval,
    Succeeded,
    Failed
}

/// <summary>
/// Individual task node within the mission's directed acyclic graph (DAG).
/// </summary>
public record MissionTaskNode(
    string TaskId,
    string Title,
    string AgentRole,
    List<string> DependsOnTaskIds,
    TaskExecutionStatus Status,
    bool RequiresHumanApproval,
    string? OutputPayload = null,
    string? FailureReason = null
);

/// <summary>
/// Represents the end-to-end autonomous mission plan and its DAG of tasks.
/// </summary>
public record AgentMissionPlan(
    Guid MissionId,
    string Title,
    string StrategicGoal,
    MissionStatus Status,
    List<MissionTaskNode> TaskDag,
    decimal EstimatedCost,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc = null
);

/// <summary>
/// Decision request submitted when reviewing a human-in-the-loop approval gate.
/// </summary>
public record MissionApprovalDecisionRequest(
    bool Approved,
    string? ReviewerNotes = null
);
