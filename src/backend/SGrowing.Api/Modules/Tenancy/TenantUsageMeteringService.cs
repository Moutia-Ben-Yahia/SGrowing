using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Tenancy;

public class TenantUsageMeteringService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<TenantUsageMeteringService>? _logger;

    public TenantUsageMeteringService(SGrowingDbContext db, ILogger<TenantUsageMeteringService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UsageMeterRecord> RecordUsageAsync(
        Guid tenantId,
        UsageMetricType metric,
        long quantity,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var record = UsageMeterRecord.Create(tenantId, metric, quantity, metadata);
        await _db.UsageMeterRecords.AddAsync(record, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogDebug("Recorded usage for tenant {TenantId}: {Metric} +{Quantity}", tenantId, metric, quantity);
        return record;
    }

    public async Task<TenantUsageSummary> GetUsageSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entitlement = await _db.FeatureEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);

        if (entitlement == null)
        {
            entitlement = FeatureEntitlement.CreateDefault(tenantId, PlanTier.Growth);
            await _db.FeatureEntitlements.AddAsync(entitlement, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var subscription = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        var cycleStart = subscription?.BillingCycleStartUtc ?? DateTime.UtcNow.AddDays(-30);

        var records = await _db.UsageMeterRecords
            .Where(r => r.TenantId == tenantId && r.RecordedAtUtc >= cycleStart)
            .ToListAsync(cancellationToken);

        long usedTokens = records.Where(r => r.Metric == UsageMetricType.AiTokens).Sum(r => r.Quantity);
        int usedVoice = (int)records.Where(r => r.Metric == UsageMetricType.VoiceMinutes).Sum(r => r.Quantity);
        int usedMessages = (int)records.Where(r => r.Metric == UsageMetricType.OutboundMessages).Sum(r => r.Quantity);

        return new TenantUsageSummary
        {
            PlanTier = entitlement.PlanTier,
            UsedAiTokens = usedTokens,
            QuotaAiTokens = entitlement.MonthlyIncludedAiTokens,
            UsedVoiceMinutes = usedVoice,
            QuotaVoiceMinutes = entitlement.MonthlyIncludedVoiceMinutes,
            UsedMessages = usedMessages,
            QuotaMessages = entitlement.MonthlyIncludedMessages
        };
    }

    public async Task<EntitlementCheckResult> CheckEntitlementAsync(
        Guid tenantId,
        UsageMetricType metric,
        long incomingQuantity,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetUsageSummaryAsync(tenantId, cancellationToken);

        long currentUsage = metric switch
        {
            UsageMetricType.AiTokens => summary.UsedAiTokens,
            UsageMetricType.VoiceMinutes => summary.UsedVoiceMinutes,
            UsageMetricType.OutboundMessages => summary.UsedMessages,
            _ => 0
        };

        long quota = metric switch
        {
            UsageMetricType.AiTokens => summary.QuotaAiTokens,
            UsageMetricType.VoiceMinutes => summary.QuotaVoiceMinutes,
            UsageMetricType.OutboundMessages => summary.QuotaMessages,
            _ => long.MaxValue
        };

        long projected = currentUsage + incomingQuantity;
        bool allowed = projected <= quota || summary.PlanTier == PlanTier.Enterprise;
        bool warning = projected >= (quota * 0.80) && projected <= quota;

        return new EntitlementCheckResult
        {
            Allowed = allowed,
            RequiresOverageWarning = warning,
            CurrentUsage = currentUsage,
            QuotaLimit = quota,
            Message = allowed
                ? (warning ? $"Warning: 80%+ of {metric} quota consumed." : "Within plan quota.")
                : $"Quota exceeded for {metric}. Please upgrade to Enterprise."
        };
    }
}
