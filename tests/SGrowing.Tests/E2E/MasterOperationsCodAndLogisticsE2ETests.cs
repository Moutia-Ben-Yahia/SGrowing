using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterOperationsCodAndLogisticsE2ETests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterE2EDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_EndToEnd_Lifecycle_From_Checkout_To_DefensiveWms_Confirmation_Logistics_And_Rma()
    {
        var db = CreateDbContext();
        var codService = new CodConfirmationService(db);
        var logisticsHub = new CourierLogisticsHubService(db);
        var wms = new DefensiveWmsService(db);
        var cadenceEngine = new SalesCadenceEngine(db);
        var idGraph = new CustomerIdentityGraphService(db);
        var portal = new CustomerSelfServicePortalService(db, logisticsHub);

        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SLIM-JACKET-BLACK-XL";

        // Step 0: Setup Courier & Inventory
        var courier = new CourierProvider
        {
            TenantId = tenantId,
            Name = "Aramex Maghreb",
            ApiCode = "ARX",
            SupportedCountries = "TN",
            IsActive = true
        };
        db.CourierProviders.Add(courier);

        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 25);

        // Step 1: Customer places COD order
        var order = CodOrder.Create(
            tenantId,
            "ORD-TUN-8801",
            "Moutia Ben Yahia",
            "+21698112233",
            "Les Berges du Lac 2",
            "Tunis",
            "La Goulette",
            140m,
            countryCode: "TN",
            codCurrency: "TND",
            shippingFee: 8m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Step 2: Risk Engine evaluates order
        var risk = await codService.EvaluateRiskAsync(tenantId, order);
        Assert.Equal(FraudRiskLevel.Low, risk.RiskLevel);
        Assert.True(risk.IsAllowedToShip);

        // Step 3: WMS reserves stock upon order placement
        await wms.ReserveStockOnCheckoutAsync(tenantId, warehouseId, sku, quantity: 1, order.Id);
        var wmsState1 = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(25, wmsState1.Physical);
        Assert.Equal(1, wmsState1.Reserved);
        Assert.Equal(24, wmsState1.Available);

        // Step 4: Anti-collision lock lease
        var lockSarah = await codService.AcquireLockAsync(tenantId, order.Id, "agent_sarah", "Sarah");
        Assert.True(lockSarah.LockAcquired);

        var lockAhmed = await codService.AcquireLockAsync(tenantId, order.Id, "agent_ahmed", "Ahmed");
        Assert.False(lockAhmed.LockAcquired); // Collision prevented

        // Step 5: Sarah records Phone confirmation
        var confirmed = await codService.RecordCallOutcomeAsync(tenantId, order.Id, "agent_sarah", "Sarah", CallOutcome.Confirmed, "Confirmed delivery at office address", null);
        Assert.Equal(CodOrderStatus.Confirmed, confirmed.Status);

        // Step 6: WMS commits stock for packing
        await wms.CommitStockOnConfirmationAsync(tenantId, warehouseId, sku, quantity: 1, order.Id);
        var wmsState2 = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(0, wmsState2.Reserved);
        Assert.Equal(1, wmsState2.Committed);

        // Step 7: Logistics Hub dispatches shipment
        var shipment = await logisticsHub.DispatchOrderAsync(tenantId, order.Id, courier.Id);
        Assert.StartsWith("ARX-ORD-TUN-8801", shipment.TrackingNumber);
        Assert.Equal(148m, shipment.CashToCollect); // 140 + 8

        // Step 8: WMS dispatches stock to courier
        await wms.DispatchStockAsync(tenantId, warehouseId, sku, quantity: 1, order.Id);
        var wmsState3 = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(24, wmsState3.Physical); // Left building
        Assert.Equal(1, wmsState3.InTransit);

        // Step 9: Courier webhook confirms delivery & cash collection
        await logisticsHub.ProcessTrackingWebhookAsync(tenantId, shipment.TrackingNumber, CourierShipmentStatus.Delivered, cashRemitted: 148m);
        Assert.Equal(CodOrderStatus.Delivered, order.Status);

        // Step 10: Public tracking portal lookup
        var tracking = await portal.LookupTrackingAsync("ORD-TUN-8801", "2233");
        Assert.Equal("ORD-TUN-8801", tracking.OrderNumber);
        Assert.Equal(148m, tracking.TotalAmount);
        Assert.Contains(tracking.TrackingTimeline, t => t.Contains("Delivered successfully"));

        // Step 11: Customer requests RMA return
        var rma = await portal.SubmitRmaRequestAsync(tenantId, "ORD-TUN-8801", "98112233", RmaReason.SizeExchange, "Size XL too big, exchange for L");
        Assert.Equal(RmaStatus.Requested, rma.Status);

        var reviewedRma = await portal.ReviewRmaRequestAsync(tenantId, rma.Id, isApproved: true, resolutionNotes: "Approved exchange", dispatchReverseCourier: true, courierId: courier.Id);
        Assert.Equal(RmaStatus.ReverseCourierDispatched, reviewedRma.Status);

        // Step 12: Sales cadence stop-on-reply check
        var cadence = await cadenceEngine.CreateCadenceAsync(tenantId, "VIP Retention Cadence", "Post-purchase nurture", "B2C VIPs");
        await cadenceEngine.AddCadenceStepAsync(cadence.Id, 1, 0, CadenceChannel.WhatsApp, null, "How is your new jacket?");
        await cadenceEngine.AddCadenceStepAsync(cadence.Id, 2, 5, CadenceChannel.Email, "Exclusive 15% discount for you", "Check out new arrivals!");

        await cadenceEngine.EnrollLeadAsync(tenantId, cadence.Id, "Moutia Ben Yahia", "moutia@sgrowing.io", "+21698112233");
        await cadenceEngine.ProcessDueCadenceStepsAsync(tenantId);

        var stopped = await cadenceEngine.HandleProspectReplyAsync(tenantId, "+21698112233", "WhatsApp", "I love the jacket, thank you!");
        Assert.True(stopped);

        // Step 13: Customer 360 profile consolidation
        var profile = await idGraph.ResolveCustomerAsync(tenantId, "Moutia Ben Yahia", "+21698112233", "moutia@sgrowing.io", CustomerChannelIdentityType.WhatsAppId, "+21698112233");
        var c360 = await idGraph.GetCustomer360Async(tenantId, profile.Id);
        Assert.True(c360.LinkedIdentities.Count >= 2);
    }
}
