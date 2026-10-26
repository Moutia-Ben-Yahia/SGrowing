using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Billing;

public record SubscriptionDetailsDto(
    Guid TenantId,
    PlanTier PlanTier,
    string Status,
    decimal MonthlyPrice,
    DateTime BillingCycleStartUtc,
    DateTime BillingCycleEndUtc,
    FeatureEntitlement Entitlements,
    TenantUsageSummary UsageSummary
);

public interface ISaasBillingService
{
    Task<SubscriptionDetailsDto> GetSubscriptionDetailsAsync(Guid tenantId);
    Task<TenantSubscription> UpgradePlanAsync(Guid tenantId, PlanTier newTier);
    Task<EntitlementCheckResult> VerifyQuotaAsync(Guid tenantId, UsageMetricType metric, int requestedQuantity = 1);
    Task<UsageMeterRecord> RecordUsageAsync(Guid tenantId, UsageMetricType metric, long quantity, string? metadata = null);
    Task<string> CreateCheckoutSessionUrlAsync(Guid tenantId, PlanTier targetTier, string successUrl);
    Task<BillingInvoiceRecord> ProcessStripePaymentSuccessWebhookAsync(Guid tenantId, string stripeInvoiceId, decimal amount, string currency = "USD");
    Task<List<BillingInvoiceRecord>> GetInvoicesAsync(Guid tenantId);
}

public class SaasBillingService : ISaasBillingService
{
    private readonly SGrowingDbContext _db;

    public SaasBillingService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<SubscriptionDetailsDto> GetSubscriptionDetailsAsync(Guid tenantId)
    {
        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        if (sub == null)
        {
            sub = TenantSubscription.Create(tenantId, PlanTier.Growth, 249m);
            _db.TenantSubscriptions.Add(sub);
            await _db.SaveChangesAsync();
        }

        var entitlement = await _db.FeatureEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId);

        if (entitlement == null)
        {
            entitlement = FeatureEntitlement.CreateDefault(tenantId, sub.PlanTier);
            _db.FeatureEntitlements.Add(entitlement);
            await _db.SaveChangesAsync();
        }

        // Calculate usage in current billing cycle
        var meterRecords = await _db.UsageMeterRecords
            .Where(m => m.TenantId == tenantId && m.RecordedAtUtc >= sub.BillingCycleStartUtc)
            .ToListAsync();

        long usedTokens = meterRecords.Where(m => m.Metric == UsageMetricType.AiTokens).Sum(m => m.Quantity);
        int usedVoice = (int)meterRecords.Where(m => m.Metric == UsageMetricType.VoiceMinutes).Sum(m => m.Quantity);
        int usedMessages = (int)meterRecords.Where(m => m.Metric == UsageMetricType.OutboundMessages).Sum(m => m.Quantity);

        var usageSummary = new TenantUsageSummary
        {
            PlanTier = sub.PlanTier,
            UsedAiTokens = usedTokens,
            QuotaAiTokens = entitlement.MonthlyIncludedAiTokens,
            UsedVoiceMinutes = usedVoice,
            QuotaVoiceMinutes = entitlement.MonthlyIncludedVoiceMinutes,
            UsedMessages = usedMessages,
            QuotaMessages = entitlement.MonthlyIncludedMessages
        };

        return new SubscriptionDetailsDto(
            tenantId,
            sub.PlanTier,
            sub.Status,
            sub.MonthlyPrice,
            sub.BillingCycleStartUtc,
            sub.BillingCycleEndUtc,
            entitlement,
            usageSummary
        );
    }

    public async Task<TenantSubscription> UpgradePlanAsync(Guid tenantId, PlanTier newTier)
    {
        var sub = await _db.TenantSubscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId);
        decimal newPrice = newTier switch
        {
            PlanTier.Starter => 99m,
            PlanTier.Enterprise => 799m,
            _ => 249m
        };

        if (sub != null)
        {
            sub.PlanTier = newTier;
            sub.MonthlyPrice = newPrice;
        }
        else
        {
            sub = TenantSubscription.Create(tenantId, newTier, newPrice);
            _db.TenantSubscriptions.Add(sub);
        }

        var entitlement = await _db.FeatureEntitlements.FirstOrDefaultAsync(e => e.TenantId == tenantId);
        if (entitlement != null)
        {
            var updated = FeatureEntitlement.CreateDefault(tenantId, newTier);
            entitlement.PlanTier = newTier;
            entitlement.MaxTeamSeats = updated.MaxTeamSeats;
            entitlement.MonthlyIncludedAiTokens = updated.MonthlyIncludedAiTokens;
            entitlement.MonthlyIncludedVoiceMinutes = updated.MonthlyIncludedVoiceMinutes;
            entitlement.MonthlyIncludedMessages = updated.MonthlyIncludedMessages;
            entitlement.IsCustomDomainEnabled = updated.IsCustomDomainEnabled;
            entitlement.IsSlaEscalationEnabled = updated.IsSlaEscalationEnabled;
        }
        else
        {
            _db.FeatureEntitlements.Add(FeatureEntitlement.CreateDefault(tenantId, newTier));
        }

        // Generate Invoice
        var invNum = $"INV-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(1000, 9999)}";
        _db.BillingInvoiceRecords.Add(BillingInvoiceRecord.Create(tenantId, invNum, newPrice, "USD", "Paid"));

        await _db.SaveChangesAsync();
        return sub;
    }

    public async Task<EntitlementCheckResult> VerifyQuotaAsync(Guid tenantId, UsageMetricType metric, int requestedQuantity = 1)
    {
        var sub = await _db.TenantSubscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId);
        var tier = sub?.PlanTier ?? PlanTier.Growth;

        var entitlement = await _db.FeatureEntitlements.FirstOrDefaultAsync(e => e.TenantId == tenantId);
        if (entitlement == null) entitlement = FeatureEntitlement.CreateDefault(tenantId, tier);

        var cycleStart = sub?.BillingCycleStartUtc ?? DateTime.UtcNow.AddDays(-30);

        long currentUsage = await _db.UsageMeterRecords
            .Where(m => m.TenantId == tenantId && m.Metric == metric && m.RecordedAtUtc >= cycleStart)
            .SumAsync(m => m.Quantity);

        long limit = metric switch
        {
            UsageMetricType.AiTokens => entitlement.MonthlyIncludedAiTokens,
            UsageMetricType.VoiceMinutes => entitlement.MonthlyIncludedVoiceMinutes,
            UsageMetricType.OutboundMessages => entitlement.MonthlyIncludedMessages,
            UsageMetricType.TeamSeats => entitlement.MaxTeamSeats,
            _ => 10_000
        };

        if (currentUsage + requestedQuantity > limit)
        {
            return new EntitlementCheckResult
            {
                Allowed = tier == PlanTier.Enterprise, // Enterprise allows overages
                RequiresOverageWarning = true,
                CurrentUsage = currentUsage,
                QuotaLimit = limit,
                Message = $"Quota limit of {limit} for {metric} exceeded. Upgrade plan to continue."
            };
        }

        return new EntitlementCheckResult
        {
            Allowed = true,
            RequiresOverageWarning = false,
            CurrentUsage = currentUsage,
            QuotaLimit = limit,
            Message = "Within plan limits"
        };
    }

    public async Task<UsageMeterRecord> RecordUsageAsync(Guid tenantId, UsageMetricType metric, long quantity, string? metadata = null)
    {
        var record = UsageMeterRecord.Create(tenantId, metric, quantity, metadata);
        _db.UsageMeterRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public Task<string> CreateCheckoutSessionUrlAsync(Guid tenantId, PlanTier targetTier, string successUrl)
    {
        // Generates realistic Stripe Checkout session redirect
        var sessionId = $"cs_test_{Guid.NewGuid().ToString("N")[..16]}";
        var priceId = targetTier switch
        {
            PlanTier.Starter => "price_starter",
            PlanTier.Enterprise => "price_enterprise",
            _ => "price_growth"
        };
        var checkoutUrl = $"https://checkout.stripe.com/c/pay/{sessionId}?price={priceId}&tenantId={tenantId}&plan={targetTier}&return_url={Uri.EscapeDataString(successUrl)}";
        return Task.FromResult(checkoutUrl);
    }

    public async Task<BillingInvoiceRecord> ProcessStripePaymentSuccessWebhookAsync(Guid tenantId, string stripeInvoiceId, decimal amount, string currency = "USD")
    {
        var invNum = $"INV-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(1000, 9999)}";
        var invoice = BillingInvoiceRecord.Create(
            tenantId,
            invNum,
            amount,
            currency,
            "Paid",
            stripeInvoiceId,
            $"https://billing.stripe.com/p/invoice/{stripeInvoiceId}"
        );

        _db.BillingInvoiceRecords.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    public async Task<List<BillingInvoiceRecord>> GetInvoicesAsync(Guid tenantId)
    {
        return await _db.BillingInvoiceRecords
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync();
    }
}
