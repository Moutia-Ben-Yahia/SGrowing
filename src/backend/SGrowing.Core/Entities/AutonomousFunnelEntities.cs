namespace SGrowing.Core.Entities;

public class AutonomousFunnelRunRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ConceptPrompt { get; set; } = string.Empty;
    public string? TargetSku { get; set; }
    public string Status { get; set; } = "Completed"; // Pending, Running, Completed, Failed
    public int CurrentStepNumber { get; set; } = 15;
    public string CurrentStepName { get; set; } = "Recurring Cron Maintenance Scheduled";
    public int ProgressPercentage { get; set; } = 100;
    public string GeneratedTitle { get; set; } = string.Empty;
    public string GeneratedHeadline { get; set; } = string.Empty;
    public string ValueProposition { get; set; } = string.Empty;
    public string GeneratedHtmlPreview { get; set; } = string.Empty;
    public int PerformanceScore { get; set; } = 98;
    public int SeoScore { get; set; } = 100;
    public int AccessibilityScore { get; set; } = 96;
    public string DeploymentUrl { get; set; } = string.Empty;
    public string? GitHubPrUrl { get; set; }
    public string? GitHubBranchName { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; } = DateTime.UtcNow;
}

public record FunnelStepExecution(
    int StepNumber,
    string StepName,
    string Status, // Completed, Running, Pending
    string OutputSummary,
    int DurationMs);

public record ExecuteAutonomousFunnelRequest(
    string ConceptPrompt,
    string? TargetSku = null);

public record AutonomousFunnelRunDetailDto(
    AutonomousFunnelRunRecord Run,
    List<FunnelStepExecution> Steps);
