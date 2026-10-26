using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class AdSpendOptimizerTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdSpendOptimizerDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task AdSpendOptimizer_DeliveredRoas_And_AutonomousRules_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new AdSpendOptimizerService(db);
        var tenantId = Guid.NewGuid();

        // 1. Ingest Ad Spend for 2 Meta Ad Sets ($100 each)
        await service.IngestAdSpendAsync(tenantId, new CampaignAdSpendRecord
        {
            TenantId = tenantId,
            Platform = AdPlatformType.Meta,
            CampaignId = "CMP-META-001",
            CampaignName = "Autumn Perfume Scaling",
            AdSetId = "ADSET-LOSER",
            AdSetName = "Broad Interest Tunisia (Low Intent)",
            SpendAmount = 100m,
            DateUtc = DateTime.UtcNow.Date
        });

        await service.IngestAdSpendAsync(tenantId, new CampaignAdSpendRecord
        {
            TenantId = tenantId,
            Platform = AdPlatformType.Meta,
            CampaignId = "CMP-META-001",
            CampaignName = "Autumn Perfume Scaling",
            AdSetId = "ADSET-WINNER",
            AdSetName = "Lookalike 1% High LTV Buyers",
            SpendAmount = 100m,
            DateUtc = DateTime.UtcNow.Date
        });

        // 2. Configure Optimizer Rules
        await service.ConfigureRuleAsync(tenantId, new AdOptimizationRule
        {
            TenantId = tenantId,
            Platform = AdPlatformType.Meta,
            RuleName = "COD Profit Protector",
            MinSpendThreshold = 50m,
            MinDeliveryRateCutoff = 55.0m,
            MinDeliveredRoasToScale = 3.5m,
            DailyBudgetScaleStepPercentage = 15.0m,
            IsAutoExecuteEnabled = true
        });

        // 3. Simulate Orders for ADSET-LOSER (10 orders placed = $450 gross GMV, but 7 returned/canceled!)
        var loserOrderIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var ord = CodOrder.Create(tenantId, $"ORD-LOSER-{i}", $"Customer {i}", "+21698111222", "Street", "Tunis", "Tunis", 45m);
            ord.Status = i < 3 ? CodOrderStatus.Delivered : CodOrderStatus.Returned; // Only 3 delivered out of 10 = 30%
            db.CodOrders.Add(ord);
            loserOrderIds.Add(ord.Id);
        }

        // 4. Simulate Orders for ADSET-WINNER (10 orders placed = $450 gross GMV, 9 delivered!)
        var winnerOrderIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var ord = CodOrder.Create(tenantId, $"ORD-WINNER-{i}", $"Customer {i}", "+21698333444", "Street", "Sfax", "Sfax", 45m);
            ord.Status = i < 9 ? CodOrderStatus.Delivered : CodOrderStatus.Returned; // 9 delivered out of 10 = 90%
            db.CodOrders.Add(ord);
            winnerOrderIds.Add(ord.Id);
        }
        await db.SaveChangesAsync();

        // 5. Calculate Delivered ROAS for Loser Ad Set
        var loserMetric = await service.CalculateDeliveredRoasAsync(
            tenantId,
            AdPlatformType.Meta,
            "ADSET-LOSER",
            "Broad Interest Tunisia (Low Intent)",
            DateTime.UtcNow.Date,
            adSpend: 100m,
            loserOrderIds);

        Assert.Equal(30.0m, loserMetric.DeliveryRatePercentage); // 30%
        Assert.Equal(4.5m, loserMetric.StandardPixelRoas); // Pixel thinks ROAS is 4.5x! ($450 / $100)
        Assert.Equal(1.35m, loserMetric.RealDeliveredRoas); // But real cash remitted was only $135 / $100 = 1.35x!
        Assert.True(loserMetric.RoasLeakagePercentage > 60m); // 70% leakage!

        // Evaluate Rule for Loser -> Auto Killswitch triggered
        var loserLog = await service.EvaluateAndApplyRulesAsync(tenantId, loserMetric.Id, currentDailyBudget: 100m);
        Assert.Equal(AdOptimizationAction.PauseAdSet, loserLog.Action);
        Assert.Equal(0m, loserLog.NewDailyBudget);
        Assert.Contains("fell below cutoff", loserLog.Reason);

        // 6. Calculate Delivered ROAS for Winner Ad Set
        var winnerMetric = await service.CalculateDeliveredRoasAsync(
            tenantId,
            AdPlatformType.Meta,
            "ADSET-WINNER",
            "Lookalike 1% High LTV Buyers",
            DateTime.UtcNow.Date,
            adSpend: 100m,
            winnerOrderIds);

        Assert.Equal(90.0m, winnerMetric.DeliveryRatePercentage); // 90%
        Assert.Equal(4.05m, winnerMetric.RealDeliveredRoas); // $405 delivered cash / $100 spend = 4.05x!

        // Evaluate Rule for Winner -> Auto Scale Multiplier triggered (+15%)
        var winnerLog = await service.EvaluateAndApplyRulesAsync(tenantId, winnerMetric.Id, currentDailyBudget: 100m);
        Assert.Equal(AdOptimizationAction.ScaleBudget, winnerLog.Action);
        Assert.Equal(115m, winnerLog.NewDailyBudget); // 100 -> 115 (+15%)
        Assert.Contains("Scaling daily budget", winnerLog.Reason);
    }
}
