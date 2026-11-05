using System;
using System.Collections.Generic;

namespace SGrowing.Core.Entities;

public class SwarmMissionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MissionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string HighLevelGoal { get; set; } = string.Empty;
    public string ScratchpadJson { get; set; } = "{}";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<SwarmTaskEntity> Tasks { get; set; } = new();
}

public class SwarmTaskEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MissionEntityId { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public string MissionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "Queued";
    public bool RequiresHumanApproval { get; set; }
    public string? OutputArtifact { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public SwarmMissionEntity? Mission { get; set; }
}
