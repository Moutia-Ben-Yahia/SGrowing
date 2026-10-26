namespace SGrowing.Core.Entities;

public enum PlanTier
{
    Starter = 1,
    Growth = 2,
    Enterprise = 3
}

public enum UsageMetricType
{
    AiTokens = 1,
    VoiceMinutes = 2,
    OutboundMessages = 3,
    TeamSeats = 4
}

public class TenantSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public PlanTier PlanTier { get; set; } = PlanTier.Growth;
    public string Status { get; set; } = "Active";
    public decimal MonthlyPrice { get; set; } = 249m;
    public DateTime BillingCycleStartUtc { get; set; } = DateTime.UtcNow;
    public DateTime BillingCycleEndUtc { get; set; } = DateTime.UtcNow.AddDays(30);

    public static TenantSubscription Create(Guid tenantId, PlanTier planTier, decimal monthlyPrice)
    {
        return new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanTier = planTier,
            Status = "Active",
            MonthlyPrice = monthlyPrice,
            BillingCycleStartUtc = DateTime.UtcNow,
            BillingCycleEndUtc = DateTime.UtcNow.AddDays(30)
        };
    }
}

public class FeatureEntitlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public PlanTier PlanTier { get; set; }
    public int MaxTeamSeats { get; set; } = 10;
    public long MonthlyIncludedAiTokens { get; set; } = 5_000_000;
    public int MonthlyIncludedVoiceMinutes { get; set; } = 1_000;
    public int MonthlyIncludedMessages { get; set; } = 10_000;
    public bool IsCustomDomainEnabled { get; set; } = true;
    public bool IsSlaEscalationEnabled { get; set; } = true;

    public static FeatureEntitlement CreateDefault(Guid tenantId, PlanTier tier)
    {
        return tier switch
        {
            PlanTier.Starter => new FeatureEntitlement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PlanTier = tier,
                MaxTeamSeats = 3,
                MonthlyIncludedAiTokens = 500_000,
                MonthlyIncludedVoiceMinutes = 100,
                MonthlyIncludedMessages = 1_000,
                IsCustomDomainEnabled = false,
                IsSlaEscalationEnabled = false
            },
            PlanTier.Enterprise => new FeatureEntitlement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PlanTier = tier,
                MaxTeamSeats = 100,
                MonthlyIncludedAiTokens = 50_000_000,
                MonthlyIncludedVoiceMinutes = 10_000,
                MonthlyIncludedMessages = 100_000,
                IsCustomDomainEnabled = true,
                IsSlaEscalationEnabled = true
            },
            _ => new FeatureEntitlement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PlanTier = PlanTier.Growth,
                MaxTeamSeats = 10,
                MonthlyIncludedAiTokens = 5_000_000,
                MonthlyIncludedVoiceMinutes = 1_000,
                MonthlyIncludedMessages = 10_000,
                IsCustomDomainEnabled = true,
                IsSlaEscalationEnabled = true
            }
        };
    }
}

public class UsageMeterRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public UsageMetricType Metric { get; set; }
    public long Quantity { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; }

    public static UsageMeterRecord Create(Guid tenantId, UsageMetricType metric, long quantity, string? metadata = null)
    {
        return new UsageMeterRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Metric = metric,
            Quantity = Math.Max(1, quantity),
            RecordedAtUtc = DateTime.UtcNow,
            Metadata = metadata
        };
    }
}

public class TenantUsageSummary
{
    public PlanTier PlanTier { get; set; }
    public long UsedAiTokens { get; set; }
    public long QuotaAiTokens { get; set; }
    public int UsedVoiceMinutes { get; set; }
    public int QuotaVoiceMinutes { get; set; }
    public int UsedMessages { get; set; }
    public int QuotaMessages { get; set; }
    public double TokenUsagePercent => QuotaAiTokens > 0 ? Math.Round((double)UsedAiTokens / QuotaAiTokens * 100, 1) : 0;
    public double VoiceUsagePercent => QuotaVoiceMinutes > 0 ? Math.Round((double)UsedVoiceMinutes / QuotaVoiceMinutes * 100, 1) : 0;
    public double MessageUsagePercent => QuotaMessages > 0 ? Math.Round((double)UsedMessages / QuotaMessages * 100, 1) : 0;
}

public class EntitlementCheckResult
{
    public bool Allowed { get; set; }
    public bool RequiresOverageWarning { get; set; }
    public long CurrentUsage { get; set; }
    public long QuotaLimit { get; set; }
    public string Message { get; set; } = string.Empty;
}
