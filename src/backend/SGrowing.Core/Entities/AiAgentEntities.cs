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
