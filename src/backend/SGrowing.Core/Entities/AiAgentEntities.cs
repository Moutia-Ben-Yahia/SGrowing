namespace SGrowing.Core.Entities;

public enum ToolApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    AutoApproved = 4
}

public class AiAgentBudget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public long MonthlyTokenLimit { get; set; } = 1_000_000;
    public long TokensConsumed { get; set; } = 0;
    public decimal MaxMonthlyCostUsd { get; set; } = 50.0m;
    public decimal CostSpentUsd { get; set; } = 0m;
    public bool IsHardCapEnforced { get; set; } = true;
    public DateTime CurrentMonthStartUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsBudgetExceeded =>
        (TokensConsumed >= MonthlyTokenLimit) || (CostSpentUsd >= MaxMonthlyCostUsd);

    public static AiAgentBudget Create(
        Guid tenantId,
        long tokenLimit = 1_000_000,
        decimal maxCostUsd = 50.0m,
        bool enforceHardCap = true)
    {
        return new AiAgentBudget
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MonthlyTokenLimit = tokenLimit,
            MaxMonthlyCostUsd = maxCostUsd,
            IsHardCapEnforced = enforceHardCap,
            CurrentMonthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}

public class AiToolExecutionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string AgentName { get; set; } = "RevenueGrowthAgent";
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "{}";
    public int ExecutionDurationMs { get; set; } = 0;
    public int TokensConsumed { get; set; } = 0;
    public decimal CostUsd { get; set; } = 0m;
    public ToolApprovalStatus ApprovalStatus { get; set; } = ToolApprovalStatus.AutoApproved;
    public string? ApprovedByUserId { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;

    public static AiToolExecutionLog Create(
        Guid tenantId,
        string agentName,
        string toolName,
        string argumentsJson,
        string resultJson,
        int durationMs,
        int tokens,
        decimal costUsd,
        ToolApprovalStatus approvalStatus = ToolApprovalStatus.AutoApproved,
        string? approvedBy = null)
    {
        return new AiToolExecutionLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentName = agentName.Trim(),
            ToolName = toolName.Trim(),
            ArgumentsJson = argumentsJson,
            ResultJson = resultJson,
            ExecutionDurationMs = durationMs,
            TokensConsumed = tokens,
            CostUsd = costUsd,
            ApprovalStatus = approvalStatus,
            ApprovedByUserId = approvedBy,
            ExecutedAtUtc = DateTime.UtcNow
        };
    }
}

public enum AgentMissionStatus
{
    Pending = 1,
    Running = 2,
    PausedForApproval = 3,
    Completed = 4,
    Failed = 5,
    Canceled = 6
}

public enum AgentMissionStepStatus
{
    Pending = 1,
    AwaitingApproval = 2,
    Executing = 3,
    Completed = 4,
    Failed = 5,
    Skipped = 6
}

public class AgentMission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TargetGoal { get; set; } = string.Empty;
    public AgentMissionStatus Status { get; set; } = AgentMissionStatus.Pending;
    public string AutonomyMode { get; set; } = "Human-in-the-Loop"; // "Auto-Approve", "Human-in-the-Loop", "Draft Only"
    public decimal TotalBudgetUsd { get; set; } = 50.0m;
    public decimal SpentBudgetUsd { get; set; } = 0m;
    public long TokensConsumed { get; set; } = 0;
    public int CurrentStepIndex { get; set; } = 0;
    public string ReasoningTrace { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public List<AgentMissionStep> Steps { get; set; } = [];

    public static AgentMission Create(
        Guid tenantId,
        string title,
        string targetGoal,
        string autonomyMode = "Human-in-the-Loop",
        decimal budgetUsd = 50.0m)
    {
        return new AgentMission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            TargetGoal = targetGoal.Trim(),
            Status = AgentMissionStatus.Pending,
            AutonomyMode = autonomyMode.Trim(),
            TotalBudgetUsd = budgetUsd,
            SpentBudgetUsd = 0m,
            TokensConsumed = 0,
            CurrentStepIndex = 0,
            ReasoningTrace = $"[Init] Mission initialized with target goal: '{targetGoal}'. Mode: {autonomyMode}.",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class AgentMissionStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MissionId { get; set; }
    public int StepIndex { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string InputArgumentsJson { get; set; } = "{}";
    public string OutputResultJson { get; set; } = "{}";
    public string ReasoningThought { get; set; } = string.Empty;
    public AgentMissionStepStatus Status { get; set; } = AgentMissionStepStatus.Pending;
    public double ExecutionDurationMs { get; set; } = 0;
    public int TokensConsumed { get; set; } = 0;
    public decimal CostUsd { get; set; } = 0m;
    public bool ApprovalRequired { get; set; } = false;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAtUtc { get; set; }

    public static AgentMissionStep Create(
        Guid missionId,
        int stepIndex,
        string toolName,
        string argumentsJson,
        string reasoningThought,
        bool approvalRequired = false)
    {
        return new AgentMissionStep
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            StepIndex = stepIndex,
            ToolName = toolName.Trim(),
            InputArgumentsJson = argumentsJson,
            ReasoningThought = reasoningThought.Trim(),
            Status = AgentMissionStepStatus.Pending,
            ApprovalRequired = approvalRequired,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

