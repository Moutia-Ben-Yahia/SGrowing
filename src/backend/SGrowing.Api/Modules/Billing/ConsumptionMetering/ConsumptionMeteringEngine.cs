using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Billing.ConsumptionMetering;

public interface IConsumptionMeteringEngine
{
    TenantConsumptionProfile IngestUsage(IngestUsageEventRequest request);
    TenantConsumptionProfile? GetUsageProfile(string tenantId);
    TenantConsumptionProfile AutoUpgradeTier(AutoUpgradeTierRequest request);
}

public class ConsumptionMeteringEngine : IConsumptionMeteringEngine
{
    private readonly ConcurrentDictionary<string, TenantConsumptionProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public ConsumptionMeteringEngine()
    {
        // Seed initial enterprise tenant
        var metrics = new List<MetricQuota>
        {
            new(UsageMetric.ApiQueries, 850000.0, 1000000.0, 85.0, 0.0005m),
            new(UsageMetric.AiTokensMillion, 42.5, 50.0, 85.0, 2.50m),
            new(UsageMetric.VoiceMinutes, 1200.0, 2000.0, 60.0, 0.08m),
            new(UsageMetric.WhatsAppMessages, 18500.0, 25000.0, 74.0, 0.02m)
        };

        _profiles["tenant_global_ent"] = new TenantConsumptionProfile(
            "tenant_global_ent",
            "Global Enterprise Tech",
            "GrowthScale Tier",
            metrics,
            EstimatedMidCycleInvoiceDollars: 2450.00m,
            OverQuotaWarningActive: true, // >= 80% on API and AI tokens
            DateTime.UtcNow.AddDays(12)
        );
    }

    public TenantConsumptionProfile IngestUsage(IngestUsageEventRequest request)
    {
        if (!_profiles.TryGetValue(request.TenantId, out var profile))
        {
            var defaultMetrics = new List<MetricQuota>
            {
                new(UsageMetric.ApiQueries, 0.0, 100000.0, 0.0, 0.001m),
                new(UsageMetric.AiTokensMillion, 0.0, 10.0, 0.0, 3.00m),
                new(UsageMetric.VoiceMinutes, 0.0, 500.0, 0.0, 0.10m),
                new(UsageMetric.WhatsAppMessages, 0.0, 5000.0, 0.0, 0.03m)
            };

            profile = new TenantConsumptionProfile(
                request.TenantId,
                request.TenantId,
                "Starter Tier",
                defaultMetrics,
                199.00m,
                false,
                DateTime.UtcNow.AddDays(30)
            );
        }

        var updatedMetrics = profile.Metrics.Select(m =>
        {
            if (m.Metric == request.Metric)
            {
                var newUsage = m.CurrentUsage + request.UnitsConsumed;
                var burn = m.TierQuotaLimit > 0 ? Math.Round((newUsage / m.TierQuotaLimit) * 100.0, 1) : 0.0;
                return m with { CurrentUsage = newUsage, BurnRatePercentage = burn };
            }
            return m;
        }).ToList();

        var anyWarning = updatedMetrics.Any(m => m.BurnRatePercentage >= 80.0);

        // Recalculate overage
        decimal basePrice = profile.CurrentTier.Contains("Growth") ? 1999m : 499m;
        decimal overage = updatedMetrics.Sum(m =>
        {
            var excess = Math.Max(0.0, m.CurrentUsage - m.TierQuotaLimit);
            return (decimal)excess * m.UnitOverageRateDollars;
        });

        var updated = profile with
        {
            Metrics = updatedMetrics,
            EstimatedMidCycleInvoiceDollars = basePrice + overage,
            OverQuotaWarningActive = anyWarning
        };

        _profiles[request.TenantId] = updated;
        return updated;
    }

    public TenantConsumptionProfile? GetUsageProfile(string tenantId)
    {
        _profiles.TryGetValue(tenantId, out var profile);
        return profile;
    }

    public TenantConsumptionProfile AutoUpgradeTier(AutoUpgradeTierRequest request)
    {
        if (!_profiles.TryGetValue(request.TenantId, out var profile))
        {
            throw new KeyNotFoundException($"Tenant {request.TenantId} not found.");
        }

        // 3x higher limits on upgrade
        var scaledMetrics = profile.Metrics.Select(m => m with
        {
            TierQuotaLimit = m.TierQuotaLimit * 2.5,
            BurnRatePercentage = Math.Round((m.CurrentUsage / (m.TierQuotaLimit * 2.5)) * 100.0, 1)
        }).ToList();

        var updated = profile with
        {
            CurrentTier = request.NewTier,
            Metrics = scaledMetrics,
            OverQuotaWarningActive = scaledMetrics.Any(m => m.BurnRatePercentage >= 80.0),
            EstimatedMidCycleInvoiceDollars = 3999.00m // Upgraded base fee
        };

        _profiles[request.TenantId] = updated;
        return updated;
    }
}
