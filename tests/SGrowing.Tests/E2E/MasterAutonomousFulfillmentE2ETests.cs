using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterAutonomousFulfillmentE2ETests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterAutonomousE2EDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_E2E_Autonomous_Commerce_Fulfillment_Lifecycle()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Setup White-Label Storefront and Custom Domain
        var domainService = new StorefrontDomainService(db);
        var domain = await domainService.RegisterCustomDomainAsync(tenantId, "store.carthage.tn");
        await domainService.VerifyDomainDnsAsync(tenantId, domain.Id, true);
        await domainService.UpsertBrandThemeTokensAsync(tenantId, new BrandThemeTokens
        {
            StorefrontSlug = "carthage",
            BrandName = "Carthage Perfumery",
            PrimaryColorHex = "#6366F1",
            SecondaryColorHex = "#14B8A6"
        });

        // 2. Setup Defensive WMS Warehouse & Inventory
        var inventory = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "ELIXIR-CARTHAGE-100",
            PhysicalStock = 25,
            ReservedStock = 0,
            CommittedStock = 0,
            InTransitStock = 0,
            QuarantineReturnedStock = 0,
            DamagedStock = 0
        };
        db.WarehouseInventories.Add(inventory);
        await db.SaveChangesAsync();

        // 3. Customer places Order via WhatsApp Native Bag (Inbound Webhook)
        var catalogEngine = new WhatsAppCatalogEngine(db);
        var orderItems = new List<WhatsAppInboundOrderItem>
        {
            new WhatsAppInboundOrderItem
            {
                ProductRetailerId = "ELIXIR-CARTHAGE-100",
                Quantity = 1,
                ItemPrice = 85.0m,
                Currency = "TND"
            }
        };

        var order = await catalogEngine.ProcessInboundWhatsAppOrderWebhookAsync(
            tenantId,
            customerPhone: "+21698765432",
            customerName: "Kais Saidi",
            catalogId: "CAT-META-CARTHAGE",
            items: orderItems,
            shippingAddress: "Boulevard 14 Janvier",
            shippingGovernorate: "Sousse");

        Assert.Equal(CodOrderStatus.PendingConfirmation, order.Status);
        Assert.Equal(92.0m, order.TotalAmount); // 85 + 7 DT shipping
        Assert.Equal(1, inventory.ReservedStock); // Stock safely reserved in Defensive WMS

        // 4. Evaluate Customer Anti-Fraud Reputation Scorecard
        var reputationService = new CustomerReputationService(db);
        var reputation = await reputationService.EvaluateCustomerReputationAsync(tenantId, "+21698765432");
        Assert.Equal(ReputationTier.Standard, reputation.Tier);

        // 5. Autonomous Voice AI Dialer: Outbound confirmation call
        var dialerService = new AutomatedVoiceDialerService(db);
        var callAttempt = await dialerService.QueueOrderForVoiceConfirmationAsync(tenantId, order.Id);
        Assert.Equal(IvrCallState.Queued, callAttempt.State);

        // Customer speaks verbal confirmation: "Oui n'akid"
        var callResult = await dialerService.ProcessIvrCustomerResponseAsync(tenantId, callAttempt.Id, "Oui n'akid", null);
        Assert.Equal(IvrCallState.Confirmed, callResult.State);

        var confirmedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.Equal(CodOrderStatus.Confirmed, confirmedOrder!.Status);
        Assert.Equal(0, inventory.ReservedStock);
        Assert.Equal(1, inventory.CommittedStock); // WMS Promoted to Committed

        // 6. Mobile Barcode Fulfillment Desk: Scan-to-Pick and Scan-to-Pack
        var fulfillmentService = new BarcodeFulfillmentService(db);
        var pickSlip = await fulfillmentService.CreatePickSlipAsync(tenantId, order.Id, warehouseId);

        // Scan barcode
        var scanResult = await fulfillmentService.ScanItemToPickSlipAsync(tenantId, pickSlip.Id, "ELIXIR-CARTHAGE-100");
        Assert.True(scanResult.Success);
        Assert.True(scanResult.IsFullyPicked);

        // Pack parcel and assign courier AWB tracking
        var packedSlip = await fulfillmentService.CompletePackAndAssignAwbAsync(tenantId, pickSlip.Id, "YAL-TN-880011", "Yalidine Express");
        Assert.Equal(PickSlipStatus.Packed, packedSlip.Status);
        Assert.Equal(0, inventory.CommittedStock);
        Assert.Equal(1, inventory.InTransitStock); // WMS Promoted to InTransit

        // 7. Dispatch Manifest: Aggregate into Courier Run-Sheet & Driver Signature
        var manifest = CourierManifestRunSheet.Create(tenantId, "Yalidine");
        db.CourierManifestRunSheets.Add(manifest);
        await db.SaveChangesAsync();

        await fulfillmentService.ScanParcelToManifestAsync(tenantId, manifest.Id, "YAL-TN-880011");
        var signedManifest = await fulfillmentService.CloseAndSignManifestAsync(
            tenantId,
            manifest.Id,
            driverName: "Bilel Ben Salah",
            driverPhone: "+21629887766",
            driverSignature: "data:image/svg+xml;base64,DRIVER_SIG_BLOB");

        Assert.Equal(ManifestStatus.HandedOverToDriver, signedManifest.Status);
        Assert.Equal(1, signedManifest.TotalParcels);

        // 8. Reverse Logistics (RMA): Uncollected parcel returned -> Barcode Inspection & Restock
        var rmaInspection = await fulfillmentService.InspectAndRestockRmaParcelAsync(
            tenantId,
            trackingNumber: "YAL-TN-880011",
            inspectorId: "QC-INSPECTOR-1",
            condition: RmaItemCondition.IntactRestock,
            notes: "Packaging intact, restocked to shelf",
            warehouseId: warehouseId);

        Assert.Equal(RmaItemCondition.IntactRestock, rmaInspection.Condition);
        Assert.Equal(0, inventory.InTransitStock);
        Assert.Equal(25, inventory.PhysicalStock); // Returned stock cleanly restored to shelf!
    }
}
