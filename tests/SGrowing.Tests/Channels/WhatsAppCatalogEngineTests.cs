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

    [Fact]
    public async Task SyncProductsToMetaCatalogAsync_UpdatesExistingMetaCatalogItem_WhenStockOrPriceChanges()
    {
        var db = CreateDbContext();
        var engine = new WhatsAppCatalogEngine(db);
        var tenantId = Guid.NewGuid();

        var product = Product.Create(tenantId, "Savon d'Alep", "savon-alep", "Savon traditionnel 200g", ProductType.Physical, 15.0m, "TND");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var variant = ProductVariant.Create(tenantId, product.Id, "ALEP-SOAP-200", "Bloc 200g", 15.0m, inventoryStock: 25);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        // Initial sync
        var feed1 = await engine.SyncProductsToMetaCatalogAsync(tenantId, "META-CAT-ALEP");
        Assert.Equal(1, feed1.TotalProducts);

        var itemBefore = await db.MetaCatalogItems.FirstOrDefaultAsync(m => m.RetailerId == "ALEP-SOAP-200");
        Assert.NotNull(itemBefore);
        Assert.Equal(15.0m, itemBefore.Price);
        Assert.Equal("in stock", itemBefore.Availability);

        // Update variant price and stock
        variant.Price = 18.5m;
        variant.InventoryStock = 10;
        await db.SaveChangesAsync();

        // Resync
        var feed2 = await engine.SyncProductsToMetaCatalogAsync(tenantId, "META-CAT-ALEP");
        Assert.Equal(1, feed2.TotalProducts);

        var itemAfter = await db.MetaCatalogItems.FirstOrDefaultAsync(m => m.RetailerId == "ALEP-SOAP-200");
        Assert.NotNull(itemAfter);
        Assert.Equal(18.5m, itemAfter.Price);
        Assert.Equal("in stock", itemAfter.Availability);
    }

    [Fact]
    public async Task SyncProductsToMetaCatalogAsync_MarksOutOfStock_WhenInventoryZero()
    {
        var db = CreateDbContext();
        var engine = new WhatsAppCatalogEngine(db);
        var tenantId = Guid.NewGuid();

        var product = Product.Create(tenantId, "Huile d'Argan", "huile-argan", "Huile pure 50ml", ProductType.Physical, 35.0m, "TND");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var variant = ProductVariant.Create(tenantId, product.Id, "ARGAN-OIL-50", "Flacon 50ml", 35.0m, inventoryStock: 0);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        var feed = await engine.SyncProductsToMetaCatalogAsync(tenantId, "META-CAT-ARGAN");
        Assert.Equal(1, feed.TotalProducts);

        var item = await db.MetaCatalogItems.FirstOrDefaultAsync(m => m.RetailerId == "ARGAN-OIL-50");
        Assert.NotNull(item);
        Assert.Equal("out of stock", item.Availability);
    }

    [Fact]
    public async Task ProcessInboundWhatsAppOrderWebhookAsync_DefaultsGovernorateAndAddress_WhenNullProvided()
    {
        var db = CreateDbContext();
        var engine = new WhatsAppCatalogEngine(db);
        var tenantId = Guid.NewGuid();

        var items = new List<WhatsAppInboundOrderItem>
        {
            new WhatsAppInboundOrderItem
            {
                ProductRetailerId = "SKU-BASIC",
                Quantity = 1,
                ItemPrice = 20.0m,
                Currency = "TND"
            }
        };

        var order = await engine.ProcessInboundWhatsAppOrderWebhookAsync(
            tenantId,
            customerPhone: "+21650112233",
            customerName: "Mohamed Ben Ali",
            catalogId: "META-DEFAULT",
            items: items,
            shippingAddress: null,
            shippingGovernorate: null
        );

        Assert.NotNull(order);
        Assert.Equal("Tunis", order.GovernorateOrWilaya);
        Assert.Equal("Adresse à confirmer via WhatsApp", order.ShippingAddress);
        Assert.Equal(27.0m, order.TotalAmount); // 20 + 7 shipping fee
    }

    [Fact]
    public async Task ProcessInboundWhatsAppOrderWebhookAsync_MultipleItems_CalculatesTotalAndReservesStockAcrossDifferentSkus()
    {
        var db = CreateDbContext();
        var engine = new WhatsAppCatalogEngine(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var inv1 = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-MULTI-1",
            PhysicalStock = 20,
            ReservedStock = 2
        };
        var inv2 = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SKU-MULTI-2",
            PhysicalStock = 10,
            ReservedStock = 0
        };
        db.WarehouseInventories.AddRange(inv1, inv2);
        await db.SaveChangesAsync();

        var items = new List<WhatsAppInboundOrderItem>
        {
            new WhatsAppInboundOrderItem { ProductRetailerId = "SKU-MULTI-1", Quantity = 3, ItemPrice = 30.0m, Currency = "TND" },
            new WhatsAppInboundOrderItem { ProductRetailerId = "SKU-MULTI-2", Quantity = 1, ItemPrice = 50.0m, Currency = "TND" }
        };

        var order = await engine.ProcessInboundWhatsAppOrderWebhookAsync(
            tenantId,
            customerPhone: "+21699887766",
            customerName: "Sonia G",
            catalogId: "META-CAT-MULTI",
            items: items,
            shippingAddress: "Route de La Marsa",
            shippingGovernorate: "Tunis"
        );

        Assert.NotNull(order);
        // (3 * 30) + (1 * 50) + 7 = 90 + 50 + 7 = 147.0
        Assert.Equal(147.0m, order.TotalAmount);

        var updatedInv1 = await db.WarehouseInventories.FindAsync(inv1.Id);
        var updatedInv2 = await db.WarehouseInventories.FindAsync(inv2.Id);

        Assert.Equal(5, updatedInv1!.ReservedStock); // 2 + 3
        Assert.Equal(1, updatedInv2!.ReservedStock); // 0 + 1
    }
}
