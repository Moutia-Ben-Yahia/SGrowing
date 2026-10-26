using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppCatalogEngineTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"WhatsAppCatalogTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task WhatsAppCatalog_Should_Sync_Products_And_Build_Mpm_Payload()
    {
        var db = CreateDbContext();
        var engine = new WhatsAppCatalogEngine(db);
        var tenantId = Guid.NewGuid();

        // 1. Seed PIM Products & Variants
        var product = Product.Create(tenantId, "Parfum Carthage", "parfum-carthage", "Eau de Parfum 100ml", ProductType.Physical, 110.0m, "TND");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var variant = ProductVariant.Create(tenantId, product.Id, "CARTHAGE-EDP-100", "Flacon 100ml", 110.0m, inventoryStock: 50);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        // 2. Sync to Meta Catalog
        var feed = await engine.SyncProductsToMetaCatalogAsync(tenantId, "META-CAT-9988");
        Assert.NotNull(feed);
        Assert.Equal(1, feed.TotalProducts);
        Assert.Equal("Success", feed.SyncStatus);

        // 3. Generate Catalog JSON Feed
        var feedJson = await engine.GenerateMetaCatalogFeedJsonAsync(tenantId);
        Assert.Contains("CARTHAGE-EDP-100", feedJson);
        Assert.Contains("Parfum Carthage - Flacon 100ml", feedJson);
        Assert.Contains("110.00 TND", feedJson);

        // 4. Build Multi-Product Message (MPM)
        var sections = new List<WhatsAppMpmSection>
        {
            new WhatsAppMpmSection
            {
                Title = "Best Sellers",
                ProductRetailerIds = new List<string> { "CARTHAGE-EDP-100" }
            }
        };

        var mpmPayload = engine.BuildMultiProductMessagePayload("+21698112233", "META-CAT-9988", "Découvrez notre collection prestige", sections);
        Assert.Contains("product_list", mpmPayload);
        Assert.Contains("META-CAT-9988", mpmPayload);
        Assert.Contains("CARTHAGE-EDP-100", mpmPayload);
    }

    [Fact]
    public async Task WhatsAppOrderWebhook_Should_Create_Order_And_Reserve_Wms_Stock()
    {
        var db = CreateDbContext();
        var engine = new WhatsAppCatalogEngine(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Seed Defensive WMS Inventory
        var inv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "DIFFUSER-OUD-200",
            PhysicalStock = 15,
            ReservedStock = 0
        };
        db.WarehouseInventories.Add(inv);
        await db.SaveChangesAsync();

        // 2. Ingest WhatsApp Order Webhook (Customer clicked Place Order in WhatsApp Bag)
        var items = new List<WhatsAppInboundOrderItem>
        {
            new WhatsAppInboundOrderItem
            {
                ProductRetailerId = "DIFFUSER-OUD-200",
                Quantity = 2,
                ItemPrice = 45.0m,
                Currency = "TND"
            }
        };

        var order = await engine.ProcessInboundWhatsAppOrderWebhookAsync(
            tenantId,
            customerPhone: "+21625447788",
            customerName: "Leila Trabelsi",
            catalogId: "META-CAT-9988",
            items: items,
            shippingAddress: "Cité Ennasr 2",
            shippingGovernorate: "Ariana");

        Assert.NotNull(order);
        Assert.Equal(CodOrderStatus.PendingConfirmation, order.Status);
        Assert.Equal(ConfirmationChannel.WhatsAppInteractive, order.ConfirmationChannel);
        Assert.Equal(97.0m, order.TotalAmount); // (45 * 2) + 7 shipping fee

        // 3. Verify Defensive WMS reserved 2 units
        var updatedInv = await db.WarehouseInventories.FindAsync(inv.Id);
        Assert.Equal(2, updatedInv!.ReservedStock);
    }
}
