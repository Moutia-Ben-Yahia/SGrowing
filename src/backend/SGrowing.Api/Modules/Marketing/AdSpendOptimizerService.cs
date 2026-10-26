using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public interface IAdSpendOptimizerService
{
    Task<CampaignAdSpendRecord> IngestAdSpendAsync(Guid tenantId, CampaignAdSpendRecord record);
    Task<AdSetDeliveredRoasMetric> CalculateDeliveredRoasAsync(
        Guid tenantId,
        AdPlatformType platform,
        string adSetId,
        string adSetName,
        DateTime date,
        decimal adSpend,
        List<Guid> attributedOrderIds);
    Task<AdOptimizationRule> ConfigureRuleAsync(Guid tenantId, AdOptimizationRule rule);
    Task<AdPlatformExecutionLog> EvaluateAndApplyRulesAsync(Guid tenantId, Guid metricId, decimal currentDailyBudget = 100m);
    Task<List<AdSetDeliveredRoasMetric>> ListRoasMetricsAsync(Guid tenantId, AdPlatformType? platform = null);
    Task<List<AdPlatformExecutionLog>> ListExecutionLogsAsync(Guid tenantId);
}

public class AdSpendOptimizerService : IAdSpendOptimizerService
{
    private readonly SGrowingDbContext _db;

    public AdSpendOptimizerService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CampaignAdSpendRecord> IngestAdSpendAsync(Guid tenantId, CampaignAdSpendRecord record)
    {
        record.TenantId = tenantId;
        _db.CampaignAdSpendRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<AdSetDeliveredRoasMetric> CalculateDeliveredRoasAsync(
        Guid tenantId,
        AdPlatformType platform,
        string adSetId,
        string adSetName,
        DateTime date,
        decimal adSpend,
        List<Guid> attributedOrderIds)
    {
        var orders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId && attributedOrderIds.Contains(o.Id))
            .ToListAsync();

        int totalOrders = orders.Count;
        int deliveredOrders = orders.Count(o => o.Status == CodOrderStatus.Delivered);
        int returnedOrders = orders.Count(o => o.Status == CodOrderStatus.Returned || o.Status == CodOrderStatus.Cancelled);

        decimal grossGmv = orders.Sum(o => o.TotalAmount);
        decimal deliveredNetCash = orders.Where(o => o.Status == CodOrderStatus.Delivered).Sum(o => o.TotalAmount);

        // Approximate COGS (30% of gross) and courier shipping/return fees
        decimal totalCogs = Math.Round(deliveredNetCash * 0.30m, 2);
        decimal courierFees = (deliveredOrders * 7.5m) + (returnedOrders * 3.5m);

        var metric = AdSetDeliveredRoasMetric.Calculate(
            tenantId,
            platform,
            adSetId,
            adSetName,
            date,
            adSpend,
            totalOrders,
            deliveredOrders,
            returnedOrders,
            grossGmv,
            deliveredNetCash,
            totalCogs,
            courierFees);

        _db.AdSetDeliveredRoasMetrics.Add(metric);
        await _db.SaveChangesAsync();
        return metric;
    }

    public async Task<AdOptimizationRule> ConfigureRuleAsync(Guid tenantId, AdOptimizationRule rule)
    {
        rule.TenantId = tenantId;
        _db.AdOptimizationRules.Add(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task<AdPlatformExecutionLog> EvaluateAndApplyRulesAsync(
        Guid tenantId,
        Guid metricId,
        decimal currentDailyBudget = 100m)
    {
        var metric = await _db.AdSetDeliveredRoasMetrics
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == metricId);

        if (metric == null)
            throw new InvalidOperationException($"Metric {metricId} not found.");

        var rule = await _db.AdOptimizationRules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Platform == metric.Platform && r.IsActive);

        var action = AdOptimizationAction.NoAction;
        string reason = "Performance within acceptable thresholds.";
        decimal? newBudget = null;

        decimal minSpend = rule?.MinSpendThreshold ?? 50.0m;
        decimal minDeliveryCutoff = rule?.MinDeliveryRateCutoff ?? 55.0m;
        decimal minRoasScale = rule?.MinDeliveredRoasToScale ?? 3.5m;
        decimal scalePct = rule?.DailyBudgetScaleStepPercentage ?? 15.0m;

        if (metric.TotalAdSpend >= minSpend)
        {
            // 1. Guard Killswitch: Low delivery clearance means ad set brings fake leads
            if (metric.DeliveryRatePercentage < minDeliveryCutoff && metric.TotalOrdersAttributed >= 5)
            {
                action = AdOptimizationAction.PauseAdSet;
                reason = $"Delivery rate ({metric.DeliveryRatePercentage}%) fell below cutoff ({minDeliveryCutoff}%). Pausing ad set to stop burning cash on uncollectible COD orders.";
                newBudget = 0m;
            }
            // 2. High-Profit Scale Multiplier: High real delivered ROAS and strong delivery rate
            else if (metric.RealDeliveredRoas >= minRoasScale && metric.DeliveryRatePercentage >= 80.0m)
            {
                action = AdOptimizationAction.ScaleBudget;
                newBudget = Math.Round(currentDailyBudget * (1m + (scalePct / 100m)), 2);
                reason = $"Real Delivered ROAS ({metric.RealDeliveredRoas}x) exceeded target ({minRoasScale}x) with {metric.DeliveryRatePercentage}% delivery clearance. Scaling daily budget +{scalePct}%.";
            }
        }

        var log = new AdPlatformExecutionLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleId = rule?.Id,
            Platform = metric.Platform,
            AdSetId = metric.AdSetId,
            Action = action,
            Reason = reason,
            CurrentDailyBudget = currentDailyBudget,
            NewDailyBudget = newBudget,
            ExecutionSuccess = true,
            ExecutedAtUtc = DateTime.UtcNow
        };

        _db.AdPlatformExecutionLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task<List<AdSetDeliveredRoasMetric>> ListRoasMetricsAsync(Guid tenantId, AdPlatformType? platform = null)
    {
        var query = _db.AdSetDeliveredRoasMetrics.Where(m => m.TenantId == tenantId);
        if (platform.HasValue)
        {
            query = query.Where(m => m.Platform == platform.Value);
        }
        return await query.OrderByDescending(m => m.DateUtc).ToListAsync();
    }

    public async Task<List<AdPlatformExecutionLog>> ListExecutionLogsAsync(Guid tenantId)
    {
        return await _db.AdPlatformExecutionLogs
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.ExecutedAtUtc)
            .ToListAsync();
    }
}
