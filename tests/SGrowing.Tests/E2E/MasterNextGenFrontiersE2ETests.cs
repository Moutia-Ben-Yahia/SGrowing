using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterNextGenFrontiersE2ETests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterNextGenFrontiersDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_NextGen_Frontiers_Unified_Commerce_Logistics_And_AdOptimizer_E2E_Should_Pass()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var hubWarehouseId = Guid.NewGuid();

        // 1. Initialize Services
        var adOptimizerService = new AdSpendOptimizerService(db);
        var flowService = new WhatsAppFlowEngineService(db);
        var liquidationService = new InventoryLiquidationService(db);

        // 2. Ingest Meta Ad Spend for Campaign
        await adOptimizerService.IngestAdSpendAsync(tenantId, new CampaignAdSpendRecord
        {
            TenantId = tenantId,
            Platform = AdPlatformType.Meta,
            CampaignId = "CAMP-SCALE-01",
            CampaignName = "Luxury Perfume Tunis",
            AdSetId = "ADSET-LAL-BUYERS",
            AdSetName = "1% Lookalike High LTV Buyers",
            SpendAmount = 100m,
            DateUtc = DateTime.UtcNow.Date
        });

        await adOptimizerService.ConfigureRuleAsync(tenantId, new AdOptimizationRule
        {
            TenantId = tenantId,
            Platform = AdPlatformType.Meta,
            RuleName = "Autopilot Scaler",
            MinSpendThreshold = 50m,
            MinDeliveryRateCutoff = 55.0m,
            MinDeliveredRoasToScale = 3.5m,
            DailyBudgetScaleStepPercentage = 15m,
            IsAutoExecuteEnabled = true
        });

        // 3. Customer places order and confirms via WhatsApp Flow Engine in Darija
        var flow = FlowDefinition.Create(tenantId, "Interactive WhatsApp Care", "START");
        await flowService.CreateFlowDefinitionAsync(tenantId, flow);

        var order = CodOrder.Create(
            tenantId,
            "ORD-TUNIS-NEXT-01",
            "Youssef Msakni",
            "+21698554433",
            "Avenue Habib Bourguiba, Appt 12",
            "Tunis",
            "Centre Ville",
            180m);
        order.Status = CodOrderStatus.PendingConfirmation;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Customer engages bot with "3aslema"
        var turn1 = await flowService.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21698554433", "3aslema");
        Assert.Equal("main_menu", turn1.NextNodeKey);

        // Customer taps "1. تأكيد الطلب"
        var turn2 = await flowService.ExecuteFlowTurnAsync(tenantId, flow.Id, "+21698554433", "1. تأكيد الطلب");
        Assert.Equal(CodOrderStatus.Confirmed, order.Status);
        Assert.Equal("OrderConfirmedInWms", turn2.ExecutedAction);

        // 4. Order is Delivered
        order.Status = CodOrderStatus.Delivered;
        await db.SaveChangesAsync();

        // 5. Closed-Loop Real Delivered ROAS calculation & Autonomous Ad Scaling
        var roasMetric = await adOptimizerService.CalculateDeliveredRoasAsync(
            tenantId,
            AdPlatformType.Meta,
            "ADSET-LAL-BUYERS",
            "1% Lookalike High LTV Buyers",
            DateTime.UtcNow.Date,
            adSpend: 100m,
            new List<Guid> { order.Id });

        Assert.Equal(100.0m, roasMetric.DeliveryRatePercentage); // 100% delivered
        Assert.True(roasMetric.RealDeliveredRoas >= 1.8m);

        // Autonomous Rule Execution
        var log = await adOptimizerService.EvaluateAndApplyRulesAsync(tenantId, roasMetric.Id, currentDailyBudget: 100m);
        Assert.NotNull(log);

        // 6. Inventory Aging & Liquidation Bundle
        var agingItem = await liquidationService.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "CREAM-FACE-AGING",
            "Hydrating Face Cream 50ml",
            hubWarehouseId,
            quantity: 40,
            costPrice: 15m,
            retailPrice: 50m,
            daysInWarehouse: 70); // Stagnant stock

        Assert.Equal(StockAgeTier.Stagnant, agingItem.Tier);
        Assert.Equal(25m, agingItem.CurrentMarkdownPercentage); // 25% discount

        var bundle = await liquidationService.GenerateLiquidationBundleAsync(
            tenantId,
            "ORD-TUNIS-NEXT-01",
            "CREAM-FACE-AGING",
            "Perfume & Luxury Cream Duo",
            bundleDiscountPct: 30m);

        Assert.True(bundle.ActiveInChatCpq);

        // 7. Worker Resilient DLQ Verification
        var scheduler = new AdvancedWorkflowSchedulerService(db);
        var deadLog = await scheduler.EnqueueDeadLetterAsync(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "node-capi-event",
            "MetaCapiSync",
            "{\"event\":\"Purchase\"}",
            "Meta Graph API HTTP 503 Service Unavailable");

        Assert.Equal(0, deadLog.RetryCount);
        deadLog.FailedAtUtc = DateTime.UtcNow.AddMinutes(-2);
        await db.SaveChangesAsync();

        int retried = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);
        Assert.Equal(1, retried);
        Assert.Equal(1, deadLog.RetryCount);
    }

    [Fact]
    public async Task Master_NextGen_Frontiers_Edge_Cases_And_Safe_Fallbacks_Should_Be_Preserved()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Ad Spend Optimizer: missing metric check
        var adOptimizer = new AdSpendOptimizerService(db);
        var missingMetricId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adOptimizer.EvaluateAndApplyRulesAsync(tenantId, missingMetricId));

        // 2. Inventory Liquidation: fresh stock receives 0% markdown and Fresh tier
        var liquidationService = new InventoryLiquidationService(db);
        var freshItem = await liquidationService.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "FRESH-LOTION-100ML",
            "Nourishing Body Lotion 100ml",
            warehouseId,
            quantity: 100,
            costPrice: 8m,
            retailPrice: 28m,
            daysInWarehouse: 12);

        Assert.Equal(StockAgeTier.Fresh, freshItem.Tier);
        Assert.Equal(0m, freshItem.CurrentMarkdownPercentage);
        Assert.Equal(28m, freshItem.CurrentEffectivePrice);

        // 3. Worker DLQ Daemon: recently failed item (within backoff window) should not be retried
        var scheduler = new AdvancedWorkflowSchedulerService(db);
        var recentDeadLog = await scheduler.EnqueueDeadLetterAsync(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "node-fresh-fail",
            "ShopifySync",
            "{}",
            "Transient Gateway Timeout 504");

        Assert.Equal(0, recentDeadLog.RetryCount);
        // FailedAtUtc is UtcNow by default, which means backoff (e.g. 1 minute) has not elapsed
        int retried = await ResilientDlqDaemon.ProcessEligibleRetriesAsync(db);
        Assert.Equal(0, retried);
        Assert.Equal(0, recentDeadLog.RetryCount);
    }

    [Fact]
    public async Task Master_NextGen_Frontiers_DeadStockTier_And_AdRuleNoAction_Evaluations()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Inventory Liquidation: stock aging > 90 days enters DeadStock tier with 45% markdown
        var liquidationService = new InventoryLiquidationService(db);
        var deadStockItem = await liquidationService.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "OBSOLETE-SERUM",
            "Aging Vitamin E Serum",
            warehouseId,
            quantity: 30,
            costPrice: 10m,
            retailPrice: 50m,
            daysInWarehouse: 110);

        Assert.Equal(StockAgeTier.DeadStock, deadStockItem.Tier);
        Assert.Equal(45m, deadStockItem.CurrentMarkdownPercentage);

        // 2. Ad Optimizer: spend below rule minimum threshold results in NoAction
        var adOptimizer = new AdSpendOptimizerService(db);
        await adOptimizer.ConfigureRuleAsync(tenantId, new AdOptimizationRule
        {
            TenantId = tenantId,
            Platform = AdPlatformType.Meta,
            RuleName = "Conservative Scale",
            MinSpendThreshold = 100m, // High spend requirement
            MinDeliveryRateCutoff = 50.0m,
            MinDeliveredRoasToScale = 3.0m,
            DailyBudgetScaleStepPercentage = 10m,
            IsAutoExecuteEnabled = true
        });

        var metric = AdSetDeliveredRoasMetric.Calculate(
            tenantId,
            AdPlatformType.Meta,
            "ADSET-LOW-SPEND",
            "Test Low Spend",
            DateTime.UtcNow.Date,
            adSpend: 25m, // Below 100m threshold
            totalOrders: 2,
            deliveredOrders: 2,
            returnedOrders: 0,
            grossGmv: 150m,
            deliveredNetCash: 150m,
            cogs: 45m,
            courierFees: 15m
        );
        db.AdSetDeliveredRoasMetrics.Add(metric);
        await db.SaveChangesAsync();

        var executionLog = await adOptimizer.EvaluateAndApplyRulesAsync(tenantId, metric.Id, currentDailyBudget: 50m);
        Assert.Equal(AdOptimizationAction.NoAction, executionLog.Action);
        Assert.Equal("Performance within acceptable thresholds.", executionLog.Reason);
    }

    [Fact]
    public async Task Master_NextGen_Frontiers_MatureAndStagnant_WarehouseAgingEvaluation()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var liquidationService = new InventoryLiquidationService(db);

        // 1. Record stock for Mature tier (45 days)
        var matureItem = await liquidationService.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "MATURE-LOTION",
            "Hydrating Body Lotion",
            warehouseId,
            quantity: 50,
            costPrice: 8m,
            retailPrice: 40m,
            daysInWarehouse: 45);

        // 2. Record stock for Stagnant tier (75 days)
        var stagnantItem = await liquidationService.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "STAGNANT-CREAM",
            "Revitalizing Night Cream",
            warehouseId,
            quantity: 25,
            costPrice: 12m,
            retailPrice: 60m,
            daysInWarehouse: 75);

        // 3. Evaluate aging across warehouse
        var evaluated = await liquidationService.EvaluateWarehouseAgingAsync(tenantId, warehouseId);
        Assert.Equal(2, evaluated.Count);

        var matureEval = evaluated.First(e => e.Sku == "MATURE-LOTION");
        Assert.Equal(StockAgeTier.Mature, matureEval.Tier);
        Assert.Equal(10m, matureEval.CurrentMarkdownPercentage);
        Assert.Equal(36m, matureEval.CurrentEffectivePrice); // 40 - 10% = 36m

        var stagnantEval = evaluated.First(e => e.Sku == "STAGNANT-CREAM");
        Assert.Equal(StockAgeTier.Stagnant, stagnantEval.Tier);
        Assert.Equal(25m, stagnantEval.CurrentMarkdownPercentage);
        Assert.Equal(45m, stagnantEval.CurrentEffectivePrice); // 60 - 25% = 45m
    }
}
