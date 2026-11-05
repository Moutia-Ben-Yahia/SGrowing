using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SGrowing.Core.Entities;

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

public static class SwarmCoordinationEntityExtensions
{
    public static MissionPlan ToModel(this SwarmMissionEntity entity)
    {
        Dictionary<string, string> scratchpad;
        try
        {
            scratchpad = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ScratchpadJson)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            scratchpad = new Dictionary<string, string>();
        }

        var tasks = entity.Tasks?.Select(t => t.ToModel()).ToList() ?? new List<SwarmTask>();

        return new MissionPlan(
            MissionId: entity.MissionId,
            TenantId: entity.TenantId,
            Title: entity.Title,
            HighLevelGoal: entity.HighLevelGoal,
            Tasks: tasks,
            Scratchpad: scratchpad,
            StartedAtUtc: entity.StartedAtUtc,
            FinishedAtUtc: entity.FinishedAtUtc
        );
    }

    public static SwarmTask ToModel(this SwarmTaskEntity entity)
    {
        Enum.TryParse<AgentPersonaRole>(entity.AssignedTo, true, out var role);
        Enum.TryParse<SwarmTaskPriority>(entity.Priority, true, out var priority);
        Enum.TryParse<SwarmTaskStatus>(entity.Status, true, out var status);

        return new SwarmTask(
            TaskId: entity.TaskId,
            MissionId: entity.MissionId,
            Title: entity.Title,
            Description: entity.Description,
            AssignedTo: role,
            Priority: priority,
            Status: status,
            RequiresHumanApproval: entity.RequiresHumanApproval,
            OutputArtifact: entity.OutputArtifact,
            CreatedAtUtc: entity.CreatedAtUtc,
            CompletedAtUtc: entity.CompletedAtUtc
        );
    }

    public static SwarmMissionEntity ToEntity(this MissionPlan model, Guid? existingId = null)
    {
        var missionEntityId = existingId ?? Guid.NewGuid();
        var entity = new SwarmMissionEntity
        {
            Id = missionEntityId,
            MissionId = model.MissionId,
            TenantId = model.TenantId,
            Title = model.Title,
            HighLevelGoal = model.HighLevelGoal,
            ScratchpadJson = JsonSerializer.Serialize(model.Scratchpad),
            StartedAtUtc = model.StartedAtUtc,
            FinishedAtUtc = model.FinishedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            Tasks = model.Tasks.Select(t => t.ToEntity(missionEntityId)).ToList()
        };

        return entity;
    }

    public static SwarmTaskEntity ToEntity(this SwarmTask model, Guid missionEntityId)
    {
        return new SwarmTaskEntity
        {
            Id = Guid.NewGuid(),
            MissionEntityId = missionEntityId,
            TaskId = model.TaskId,
            MissionId = model.MissionId,
            Title = model.Title,
            Description = model.Description,
            AssignedTo = model.AssignedTo.ToString(),
            Priority = model.Priority.ToString(),
            Status = model.Status.ToString(),
            RequiresHumanApproval = model.RequiresHumanApproval,
            OutputArtifact = model.OutputArtifact,
            CreatedAtUtc = model.CreatedAtUtc,
            CompletedAtUtc = model.CompletedAtUtc
        };
    }
}

