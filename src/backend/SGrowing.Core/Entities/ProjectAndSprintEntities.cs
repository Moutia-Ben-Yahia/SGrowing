using System.Text.Json.Serialization;

namespace SGrowing.Core.Entities;

public class ProjectRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active, Completed, OnHold
    public Guid? DealId { get; set; }
    public Guid? ContractId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? TargetLaunchDateUtc { get; set; }
}

public class TaskItemRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Backlog"; // Backlog, Todo, InProgress, InReview, Done
    public string Priority { get; set; } = "Medium"; // Urgent, High, Medium, Low
    public string? AssigneeName { get; set; }
    public string? AssigneeAvatar { get; set; }
    public int Position { get; set; }
    public int SubtasksCompleted { get; set; }
    public int SubtasksTotal { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TaskCommentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid TenantId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CreateTaskRequest(
    string Title,
    string Description,
    string Status,
    string Priority,
    string? AssigneeName,
    int SubtasksCompleted = 0,
    int SubtasksTotal = 0,
    DateTime? DueDateUtc = null);

public record UpdateTaskRequest(
    string? Title = null,
    string? Description = null,
    string? Status = null,
    string? Priority = null,
    string? AssigneeName = null,
    int? Position = null,
    int? SubtasksCompleted = null,
    int? SubtasksTotal = null,
    DateTime? DueDateUtc = null);

public record AddCommentRequest(
    string AuthorName,
    string Content);

public record ConvertDealRequest(
    Guid DealId);

public record ConvertContractRequest(
    Guid ContractId);
