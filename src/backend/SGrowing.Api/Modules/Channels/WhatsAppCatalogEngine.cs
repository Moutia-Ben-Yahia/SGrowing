using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels;

public interface IWhatsAppCatalogEngine
{
    Task<MetaCatalogSyncFeed> SyncProductsToMetaCatalogAsync(Guid tenantId, string catalogId);
    Task<string> GenerateMetaCatalogFeedJsonAsync(Guid tenantId);
    string BuildMultiProductMessagePayload(string recipientPhone, string catalogId, string bodyText, List<WhatsAppMpmSection> sections);
    Task<CodOrder> ProcessInboundWhatsAppOrderWebhookAsync(
        Guid tenantId,
        string customerPhone,
        string customerName,
        string catalogId,
        List<WhatsAppInboundOrderItem> items,
        string? shippingAddress,
        string? shippingGovernorate);
}

public class WhatsAppCatalogEngine : IWhatsAppCatalogEngine
{
    private readonly SGrowingDbContext _db;

    public WhatsAppCatalogEngine(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<MetaCatalogSyncFeed> SyncProductsToMetaCatalogAsync(Guid tenantId, string catalogId)
    {
        var products = await _db.Products
            .Where(p => p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.Id);

        var variants = await _db.ProductVariants
            .Where(v => v.TenantId == tenantId)
            .ToListAsync();

        var existingItems = await _db.MetaCatalogItems
            .Where(m => m.TenantId == tenantId)
            .ToListAsync();

        foreach (var variant in variants)
        {
            products.TryGetValue(variant.ProductId, out var product);
            var prodName = product?.Name ?? "Product";
            var prodDesc = product?.Description ?? prodName;
            var prodCurrency = product?.Currency ?? "TND";

            var item = existingItems.FirstOrDefault(e => e.RetailerId == variant.SKU);
            if (item == null)
            {
                item = new MetaCatalogItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RetailerId = variant.SKU,
                    Title = $"{prodName} - {variant.Name}",
                    Description = prodDesc,
                    Price = variant.Price,
                    Currency = prodCurrency,
                    Availability = variant.InventoryStock > 0 ? "in stock" : "out of stock",
                    Condition = "new",
                    Brand = "SGrowing Merchant",
                    ImageLink = "https://cdn.sgrowing.com/placeholder.jpg",
                    LastUpdatedUtc = DateTime.UtcNow
                };
                _db.MetaCatalogItems.Add(item);
            }
            else
            {
                item.Title = $"{prodName} - {variant.Name}";
                item.Price = variant.Price;
                item.Currency = prodCurrency;
                item.Availability = variant.InventoryStock > 0 ? "in stock" : "out of stock";
                item.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        var feed = await _db.MetaCatalogSyncFeeds.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.CatalogId == catalogId);
        if (feed == null)
        {
            feed = new MetaCatalogSyncFeed
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CatalogId = catalogId,
                FeedFormat = "JSON",
                TotalProducts = variants.Count,
                LastSyncedAtUtc = DateTime.UtcNow,
                SyncStatus = "Success"
            };
            _db.MetaCatalogSyncFeeds.Add(feed);
        }
        else
        {
            feed.TotalProducts = variants.Count;
            feed.LastSyncedAtUtc = DateTime.UtcNow;
            feed.SyncStatus = "Success";
        }

        await _db.SaveChangesAsync();
        return feed;
    }

    public async Task<string> GenerateMetaCatalogFeedJsonAsync(Guid tenantId)
    {
        var items = await _db.MetaCatalogItems
            .Where(m => m.TenantId == tenantId)
            .Select(m => new
            {
                id = m.RetailerId,
                title = m.Title,
                description = m.Description,
                availability = m.Availability,
                condition = m.Condition,
                price = $"{m.Price:F2} {m.Currency}",
                link = m.Link,
                image_link = m.ImageLink,
                brand = m.Brand,
                google_product_category = m.GoogleProductCategory
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new { data = items }, new JsonSerializerOptions { WriteIndented = true });
    }

    public string BuildMultiProductMessagePayload(string recipientPhone, string catalogId, string bodyText, List<WhatsAppMpmSection> sections)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = recipientPhone,
            type = "interactive",
            interactive = new
            {
                type = "product_list",
                header = new
                {
                    type = "text",
                    text = "Boutique SGrowing"
                },
                body = new
                {
                    text = bodyText
                },
                footer = new
                {
                    text = "Livraison express 24-48h sur toute la Tunisie"
                },
                action = new
                {
                    catalog_id = catalogId,
                    sections = sections.Select(s => new
                    {
                        title = s.Title,
                        product_items = s.ProductRetailerIds.Select(sku => new
                        {
                            product_retailer_id = sku
                        }).ToArray()
                    }).ToArray()
                }
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<CodOrder> ProcessInboundWhatsAppOrderWebhookAsync(
        Guid tenantId,
        string customerPhone,
        string customerName,
        string catalogId,
        List<WhatsAppInboundOrderItem> items,
        string? shippingAddress,
        string? shippingGovernorate)
    {
        decimal itemsTotal = items.Sum(i => i.ItemPrice * i.Quantity);
        decimal shippingFee = 7.0m;
        decimal totalAmount = itemsTotal + shippingFee;
        var gov = string.IsNullOrWhiteSpace(shippingGovernorate) ? "Tunis" : shippingGovernorate;
        var address = string.IsNullOrWhiteSpace(shippingAddress) ? "Adresse à confirmer via WhatsApp" : shippingAddress;
        var orderNumber = $"WA-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

        var itemsJson = JsonSerializer.Serialize(items.Select(i => new
        {
            sku = i.ProductRetailerId,
            title = $"Catalogue Item {i.ProductRetailerId}",
            quantity = i.Quantity,
            unitPrice = i.ItemPrice
        }));

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: orderNumber,
            customerName: customerName,
            phoneNumber: customerPhone,
            shippingAddress: address,
            governorateOrWilaya: gov,
            delegationOrCommune: gov,
            totalAmount: totalAmount,
            countryCode: "TN",
            codCurrency: items.FirstOrDefault()?.Currency ?? "TND",
            shippingFee: shippingFee,
            itemsJson: itemsJson,
            notes: $"Generated from WhatsApp Native Cart (Catalog: {catalogId})");

        order.ConfirmationChannel = ConfirmationChannel.WhatsAppInteractive;
        order.Status = CodOrderStatus.PendingConfirmation;

        // Defensive WMS: Reserve inventory for these items
        var inventories = await _db.WarehouseInventories.Where(w => w.TenantId == tenantId).ToListAsync();
        foreach (var item in items)
        {
            var inv = inventories.FirstOrDefault(i => i.Sku == item.ProductRetailerId) ?? inventories.FirstOrDefault();
            if (inv != null)
            {
                inv.ReservedStock += item.Quantity;
                inv.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        _db.CodOrders.Add(order);
        await _db.SaveChangesAsync();

        return order;
    }
}
