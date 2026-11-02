namespace SGrowing.Api.Modules.AI.SwarmCoordination;

public enum AgentPersonaRole
{
    GrowthDirector,
    LeadResearcher,
    OutreachCopywriter,
    AdOptimizer,
    TechnicalSeoAuditor,
    ComplianceGuardian
}

public enum SwarmTaskPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum SwarmTaskStatus
{
    Queued,
    Assigned,
    Executing,
    AwaitingApproval,
    Completed,
    Rejected,
    Failed
}

public record SwarmTask(
    string TaskId,
    string MissionId,
    string Title,
    string Description,
    AgentPersonaRole AssignedTo,
    SwarmTaskPriority Priority,
    SwarmTaskStatus Status,
    bool RequiresHumanApproval,
    string? OutputArtifact,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc
);

public record MissionPlan(
    string MissionId,
    string TenantId,
    string Title,
    string HighLevelGoal,
    List<SwarmTask> Tasks,
    Dictionary<string, string> Scratchpad,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc
);

public record CreateMissionRequest(
    string TenantId,
    string Title,
    string HighLevelGoal,
    List<string>? FocusAreas = null
);

public record ApproveTaskRequest(
    string TenantId,
    string MissionId,
    string TaskId,
    bool Approved,
    string ApproverId,
    string? Reason = null
);

public record UpdateScratchpadRequest(
    string TenantId,
    string MissionId,
    string Key,
    string Value
);

public record SwarmTelemetrySummary(
    string MissionId,
    int TotalTasks,
    int CompletedTasks,
    int PendingApprovalCount,
    Dictionary<string, string> Scratchpad
);
