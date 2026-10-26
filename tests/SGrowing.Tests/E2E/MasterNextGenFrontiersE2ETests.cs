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
        var fleetService = new InHouseFleetService(db);
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

        // 4. In-House Fleet Delivery Dispatch
        var driver = DeliveryDriver.Create(tenantId, "Kais Driver", "+21622112233", "Van", "190-TN-221", hubWarehouseId);
        await fleetService.RegisterDriverAsync(tenantId, driver);

        var runSheet = await fleetService.GenerateRunSheetAsync(tenantId, driver.Id, hubWarehouseId, new List<Guid> { order.Id });
        var stop = await db.RunSheetStops.FirstAsync(s => s.RunSheetId == runSheet.Id);

        // Doorstep Delivery & Cash Collected (180 TND)
        await fleetService.RecordStopDeliveryAsync(
            tenantId,
            stop.Id,
            StopDeliveryStatus.DeliveredPaidCash,
            paymentMethod: "CashOnDelivery",
            signatureData: "signature_data_ok",
            lat: 36.8065,
            lon: 10.1815);

        Assert.Equal(CodOrderStatus.Delivered, order.Status);
        Assert.Equal(180m, driver.CashInHandBalance);

        // End of Day Cash Drawer Reconciliation
        var recon = await fleetService.ReconcileDriverCashAsync(tenantId, runSheet.Id, 180m, "Cashier Sarah");
        Assert.True(recon.IsDiscrepancyZero);
        Assert.Equal(0m, driver.CashInHandBalance);

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
}
