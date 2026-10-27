using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Billing;

public interface IPlanQuotaEnforcementService
{
    Task<PlanQuotaEnforcementResult> CheckQuotaAsync(Guid tenantId, UsageMetricType metric, long requestedQuantity = 1, CancellationToken cancellationToken = default);
    Task<UsageMeterRecord> ConsumeQuotaAsync(Guid tenantId, UsageMetricType metric, long quantity, string? metadata = null, CancellationToken cancellationToken = default);
    Task<TenantQuotaStatusDto> GetTenantQuotaStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public class PlanQuotaEnforcementService : IPlanQuotaEnforcementService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<PlanQuotaEnforcementService>? _logger;

    public PlanQuotaEnforcementService(SGrowingDbContext db, ILogger<PlanQuotaEnforcementService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PlanQuotaEnforcementResult> CheckQuotaAsync(
        Guid tenantId,
        UsageMetricType metric,
        long requestedQuantity = 1,
        CancellationToken cancellationToken = default)
    {
        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        var tier = sub?.PlanTier ?? PlanTier.Growth;

        var entitlement = await _db.FeatureEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);
        if (entitlement == null)
        {
            entitlement = FeatureEntitlement.CreateDefault(tenantId, tier);
        }

        var cycleStart = sub?.BillingCycleStartUtc ?? DateTime.UtcNow.AddDays(-30);

        long currentUsage = await _db.UsageMeterRecords
            .Where(m => m.TenantId == tenantId && m.Metric == metric && m.RecordedAtUtc >= cycleStart)
            .SumAsync(m => m.Quantity, cancellationToken);

        long quotaLimit = metric switch
        {
            UsageMetricType.ConnectedWarmupMailboxes => entitlement.MaxWarmupMailboxes,
            UsageMetricType.MonthlyColdEmails => entitlement.MonthlyIncludedColdEmails,
            UsageMetricType.CarrierWaybills => entitlement.MaxCarrierWaybills,
            UsageMetricType.StorageMb => entitlement.StorageMbLimit,
            UsageMetricType.AiTokens => entitlement.MonthlyIncludedAiTokens,
            UsageMetricType.VoiceMinutes => entitlement.MonthlyIncludedVoiceMinutes,
            UsageMetricType.OutboundMessages => entitlement.MonthlyIncludedMessages,
            UsageMetricType.TeamSeats => entitlement.MaxTeamSeats,
            _ => 10_000
        };

        bool wouldExceed = (currentUsage + requestedQuantity) > quotaLimit;

        // Tiered Policy:
        // Starter: Hard limit (blocks completely)
        // Growth: Soft limit (allows with overage warning)
        // Enterprise: Elastic limit (unlimited overage permitted)
        if (tier == PlanTier.Starter && wouldExceed)
        {
            _logger?.LogWarning("Tenant {TenantId} on Starter plan reached HARD quota cap for {Metric}. Used: {Used}, Limit: {Limit}",
                tenantId, metric, currentUsage, quotaLimit);

            return new PlanQuotaEnforcementResult
            {
                Allowed = false,
                IsHardBlocked = true,
                RequiresOverageWarning = false,
                PlanTier = tier,
                Metric = metric,
                CurrentUsage = currentUsage,
                RequestedQuantity = requestedQuantity,
                QuotaLimit = quotaLimit,
                Message = $"Starter plan hard quota of {quotaLimit:N0} for {metric} exceeded (Current: {currentUsage:N0}). Upgrade to Growth or Enterprise to continue."
            };
        }

        if (tier == PlanTier.Growth && wouldExceed)
        {
            _logger?.LogInformation("Tenant {TenantId} on Growth plan exceeded soft quota for {Metric}. Allowed with overage.",
                tenantId, metric);

            return new PlanQuotaEnforcementResult
            {
                Allowed = true,
                IsHardBlocked = false,
                RequiresOverageWarning = true,
                PlanTier = tier,
                Metric = metric,
                CurrentUsage = currentUsage,
                RequestedQuantity = requestedQuantity,
                QuotaLimit = quotaLimit,
                Message = $"Growth plan soft quota of {quotaLimit:N0} for {metric} reached. Additional usage will incur standard metered overage charges."
            };
        }

        if (tier == PlanTier.Enterprise && wouldExceed)
        {
            _logger?.LogInformation("Tenant {TenantId} on Enterprise plan scaling elastically for {Metric}.",
                tenantId, metric);

            return new PlanQuotaEnforcementResult
            {
                Allowed = true,
                IsHardBlocked = false,
                RequiresOverageWarning = false,
                PlanTier = tier,
                Metric = metric,
                CurrentUsage = currentUsage,
                RequestedQuantity = requestedQuantity,
                QuotaLimit = quotaLimit,
                Message = $"Enterprise plan elastic quota exceeded ({currentUsage:N0}/{quotaLimit:N0}). Elastic auto-scaling enabled."
            };
        }

        return new PlanQuotaEnforcementResult
        {
            Allowed = true,
            IsHardBlocked = false,
            RequiresOverageWarning = false,
            PlanTier = tier,
            Metric = metric,
            CurrentUsage = currentUsage,
            RequestedQuantity = requestedQuantity,
            QuotaLimit = quotaLimit,
            Message = "Within plan entitlement quota limits."
        };
    }

    public async Task<UsageMeterRecord> ConsumeQuotaAsync(
        Guid tenantId,
        UsageMetricType metric,
        long quantity,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var record = UsageMeterRecord.Create(tenantId, metric, quantity, metadata);
        await _db.UsageMeterRecords.AddAsync(record, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return record;
    }

    public async Task<TenantQuotaStatusDto> GetTenantQuotaStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        var tier = sub?.PlanTier ?? PlanTier.Growth;

        var entitlement = await _db.FeatureEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken)
            ?? FeatureEntitlement.CreateDefault(tenantId, tier);

        var cycleStart = sub?.BillingCycleStartUtc ?? DateTime.UtcNow.AddDays(-30);

        var recentRecords = await _db.UsageMeterRecords
            .Where(m => m.TenantId == tenantId && m.RecordedAtUtc >= cycleStart)
            .ToListAsync(cancellationToken);

        var metricsList = new List<TenantQuotaMetricStatus>
        {
            new()
            {
                Metric = UsageMetricType.ConnectedWarmupMailboxes,
                MetricName = "Connected Warmup Mailboxes",
                Used = recentRecords.Where(r => r.Metric == UsageMetricType.ConnectedWarmupMailboxes).Sum(r => r.Quantity),
                Limit = entitlement.MaxWarmupMailboxes
            },
            new()
            {
                Metric = UsageMetricType.MonthlyColdEmails,
                MetricName = "Monthly Cold Emails",
                Used = recentRecords.Where(r => r.Metric == UsageMetricType.MonthlyColdEmails).Sum(r => r.Quantity),
                Limit = entitlement.MonthlyIncludedColdEmails
            },
            new()
            {
                Metric = UsageMetricType.CarrierWaybills,
                MetricName = "Carrier Waybills",
                Used = recentRecords.Where(r => r.Metric == UsageMetricType.CarrierWaybills).Sum(r => r.Quantity),
                Limit = entitlement.MaxCarrierWaybills
            },
            new()
            {
                Metric = UsageMetricType.StorageMb,
                MetricName = "Storage (MB)",
                Used = recentRecords.Where(r => r.Metric == UsageMetricType.StorageMb).Sum(r => r.Quantity),
                Limit = entitlement.StorageMbLimit
            },
            new()
            {
                Metric = UsageMetricType.AiTokens,
                MetricName = "AI LLM Tokens",
                Used = recentRecords.Where(r => r.Metric == UsageMetricType.AiTokens).Sum(r => r.Quantity),
                Limit = entitlement.MonthlyIncludedAiTokens
            },
            new()
            {
                Metric = UsageMetricType.VoiceMinutes,
                MetricName = "Voice Telephony Minutes",
                Used = recentRecords.Where(r => r.Metric == UsageMetricType.VoiceMinutes).Sum(r => r.Quantity),
                Limit = entitlement.MonthlyIncludedVoiceMinutes
            }
        };

        return new TenantQuotaStatusDto
        {
            TenantId = tenantId,
            PlanTier = tier,
            Metrics = metricsList
        };
    }
}
