namespace SGrowing.Api.Modules.Agents.GrowthDirector;

public static class GrowthAgentTypes
{
    public const string SeoSpecialist = "SEO_Specialist";
    public const string ContentCopywriter = "Content_Copywriter";
    public const string AdOptimizer = "Ad_Optimizer";
    public const string AutomationArchitect = "Automation_Architect";
}

public static class GrowthMissionStatus
{
    public const string PendingApproval = "PendingApproval";
    public const string AutoApproved = "AutoApproved";
    public const string PartiallyExecuted = "PartiallyExecuted";
    public const string Executed = "Executed";
}

public static class GrowthTaskStatus
{
    public const string PendingApproval = "PendingApproval";
    public const string AutoApproved = "AutoApproved";
    public const string Executed = "Executed";
}

public record GrowthMissionObjective(
    string ObjectiveId,
    string TenantId,
    string Title,
    string TargetMetric,
    decimal TargetValue,
    decimal MaxBudgetUsd,
    DateTime DeadlineUtc
);

public record DecomposedSubTask(
    string TaskId,
    string AgentType,
    string Title,
    string Description,
    decimal AllocatedBudget,
    bool RequiresHumanApproval,
    string Status
);

public record FinancialGuardrailPolicy(
    decimal AutoApproveSpendLimitUsd = 250m,
    decimal MaxDailyBudgetUsd = 2000m,
    bool RequireHitlForAudienceSync = true
);

public record GrowthMissionPlan(
    string MissionId,
    GrowthMissionObjective Objective,
    List<DecomposedSubTask> SubTasks,
    decimal TotalEstimatedCostUsd,
    bool ExceedsAutoApprovalThreshold,
    string Status,
    DateTime CreatedAtUtc
);

public record ExecuteMissionResult(
    bool Success,
    string MissionId,
    int TasksExecuted,
    int TasksPendingApproval,
    decimal TotalApprovedSpend,
    string StatusMessage,
    DateTime ExecutedAtUtc
);

public record DecomposeObjectiveRequest(
    GrowthMissionObjective? Objective = null,
    FinancialGuardrailPolicy? Policy = null,
    string? ObjectiveId = null,
    string? TenantId = null,
    string? Title = null,
    string? TargetMetric = null,
    decimal? TargetValue = null,
    decimal? MaxBudgetUsd = null,
    DateTime? DeadlineUtc = null
)
{
    public GrowthMissionObjective? ResolveObjective()
    {
        if (Objective is not null) return Objective;
        if (!string.IsNullOrWhiteSpace(Title))
        {
            return new GrowthMissionObjective(
                ObjectiveId ?? Guid.NewGuid().ToString("N")[..8],
                TenantId ?? "default-tenant",
                Title,
                TargetMetric ?? "Conversions",
                TargetValue ?? 100m,
                MaxBudgetUsd ?? 1000m,
                DeadlineUtc ?? DateTime.UtcNow.AddMonths(1)
            );
        }
        return null;
    }
}

public record ApproveAndExecuteRequest(
    string TenantId,
    string MissionId,
    List<string>? ApprovedTaskIds = null
);
