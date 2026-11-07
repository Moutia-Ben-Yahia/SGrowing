using System.Text.Json.Serialization;

namespace SGrowing.Client.Models;

/// <summary>
/// High-level marketing and growth objective provided to the autonomous agent swarm.
/// </summary>
public record MissionObjectiveRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("strategicGoal")] string StrategicGoal,
    [property: JsonPropertyName("budgetLimit")] decimal BudgetLimit,
    [property: JsonPropertyName("targetGeo")] string TargetGeo,
    [property: JsonPropertyName("constraints")] List<string>? Constraints = null
);

/// <summary>
/// Lifecycle status of an autonomous mission plan.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
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
[JsonConverter(typeof(JsonStringEnumConverter))]
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
    [property: JsonPropertyName("taskId")] string TaskId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("agentRole")] string AgentRole,
    [property: JsonPropertyName("dependsOnTaskIds")] List<string> DependsOnTaskIds,
    [property: JsonPropertyName("status")] TaskExecutionStatus Status,
    [property: JsonPropertyName("requiresHumanApproval")] bool RequiresHumanApproval,
    [property: JsonPropertyName("outputPayload")] string? OutputPayload = null,
    [property: JsonPropertyName("failureReason")] string? FailureReason = null
);

/// <summary>
/// Represents the end-to-end autonomous mission plan and its DAG of tasks.
/// </summary>
public record AgentMissionPlan(
    [property: JsonPropertyName("missionId")] Guid MissionId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("strategicGoal")] string StrategicGoal,
    [property: JsonPropertyName("status")] MissionStatus Status,
    [property: JsonPropertyName("taskDag")] List<MissionTaskNode> TaskDag,
    [property: JsonPropertyName("estimatedCost")] decimal EstimatedCost,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("completedAtUtc")] DateTime? CompletedAtUtc = null
);

/// <summary>
/// Decision request submitted when reviewing a human-in-the-loop approval gate.
/// </summary>
public record MissionApprovalDecisionRequest(
    [property: JsonPropertyName("approved")] bool Approved,
    [property: JsonPropertyName("reviewerNotes")] string? ReviewerNotes = null
);
