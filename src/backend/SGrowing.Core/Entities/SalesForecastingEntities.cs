namespace SGrowing.Core.Entities;

public enum ForecastCategory
{
    Pipeline = 1,
    BestCase = 2,
    Commit = 3,
    Closed = 4,
    Omitted = 5
}

public class SalesForecastPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Q4 2026", "November 2026"
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public decimal TargetQuotaAmount { get; set; } = 0m;
    public decimal ClosedWonRevenue { get; set; } = 0m;
    public decimal WeightedForecastRevenue { get; set; } = 0m;
    public decimal UnweightedPipelineRevenue { get; set; } = 0m;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static SalesForecastPeriod Create(
        Guid tenantId,
        string name,
        DateTime startDateUtc,
        DateTime endDateUtc,
        decimal targetQuota)
    {
        return new SalesForecastPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            TargetQuotaAmount = targetQuota,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class SalesForecastRepQuota
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid SalesRepUserId { get; set; }
    public string SalesRepName { get; set; } = string.Empty;
    public decimal QuotaAmount { get; set; } = 0m;
    public decimal ClosedWonAmount { get; set; } = 0m;
    public decimal CommitAmount { get; set; } = 0m;
    public decimal BestCaseAmount { get; set; } = 0m;
    public decimal PipelineAmount { get; set; } = 0m;
    public double AttainmentPercentage => QuotaAmount > 0 ? (double)(ClosedWonAmount / QuotaAmount) * 100.0 : 0.0;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SalesCommissionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal BaseRatePercent { get; set; } = 10.0m; // 10%
    public decimal QuotaThresholdPercent { get; set; } = 100.0m;
    public decimal AcceleratedRatePercent { get; set; } = 15.0m; // 15% above quota
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static SalesCommissionPlan Create(
        Guid tenantId,
        string planName,
        decimal baseRate = 10.0m,
        decimal threshold = 100.0m,
        decimal acceleratedRate = 15.0m)
    {
        return new SalesCommissionPlan
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanName = planName.Trim(),
            BaseRatePercent = baseRate,
            QuotaThresholdPercent = threshold,
            AcceleratedRatePercent = acceleratedRate,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public enum CommissionStatementStatus
{
    Draft = 1,
    Approved = 2,
    Paid = 3
}

public class SalesCommissionStatement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid SalesRepUserId { get; set; }
    public string SalesRepName { get; set; } = string.Empty;
    public int DealsClosedCount { get; set; } = 0;
    public decimal TotalDealsValue { get; set; } = 0m;
    public decimal CalculatedCommission { get; set; } = 0m;
    public CommissionStatementStatus Status { get; set; } = CommissionStatementStatus.Draft;
    public DateTime? ApprovedAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static SalesCommissionStatement Create(
        Guid tenantId,
        Guid periodId,
        Guid salesRepUserId,
        string salesRepName,
        int dealsClosed,
        decimal totalDealsValue,
        decimal commission)
    {
        return new SalesCommissionStatement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PeriodId = periodId,
            SalesRepUserId = salesRepUserId,
            SalesRepName = salesRepName.Trim(),
            DealsClosedCount = dealsClosed,
            TotalDealsValue = totalDealsValue,
            CalculatedCommission = commission,
            Status = CommissionStatementStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
