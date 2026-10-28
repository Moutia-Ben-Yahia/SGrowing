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

public class ClientPortalTokenRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string ClientContactEmail { get; set; } = string.Empty;
    public string ClientOrganizationName { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; } = false;
}

public class ClientPortalFeedbackRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string StatusVote { get; set; } = "Approved"; // Approved, ChangeRequested, InReview
    public string FeedbackNotes { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}

public record GeneratePortalTokenRequest(
    string? ClientEmail = null,
    string? ClientOrganization = null,
    int ValidDays = 30);

public record ClientPortalTokenResult(
    string Token,
    string ShareableUrl,
    DateTime ExpiresAtUtc,
    string ClientOrganizationName);

public record ClientPortalTaskView(
    Guid Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    int SubtasksCompleted,
    int SubtasksTotal,
    int ProgressPercent,
    DateTime? DueDateUtc,
    string? LatestClientVote);

public record ClientPortalView(
    Guid ProjectId,
    string ProjectName,
    string ProjectDescription,
    string ClientOrganizationName,
    int OverallProgressPercent,
    int CompletedMilestones,
    int TotalMilestones,
    DateTime? TargetLaunchDateUtc,
    DateTime TokenExpiresAtUtc,
    List<ClientPortalTaskView> Milestones,
    List<TaskCommentRecord> RecentUpdates);

public record ClientPortalFeedbackRequest(
    Guid TaskId,
    string ClientName,
    string ClientEmail,
    string StatusVote,
    string FeedbackNotes);

public record CompleteMilestoneRequest(
    bool TriggerInvoice = true,
    decimal? MilestoneAmount = null,
    string Currency = "USD");

public record CompleteMilestoneResult(
    TaskItemRecord Task,
    InvoiceRecord? Invoice,
    string Message);

