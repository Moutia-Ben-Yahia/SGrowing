namespace SGrowing.Core.Entities;

public class MetaCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RetailerId { get; set; } = string.Empty; // SKU
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Availability { get; set; } = "in stock"; // "in stock", "out of stock"
    public string Condition { get; set; } = "new";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TND";
    public string Link { get; set; } = string.Empty;
    public string ImageLink { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string GoogleProductCategory { get; set; } = "Health & Beauty > Personal Care > Cosmetics > Perfume";
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class MetaCatalogSyncFeed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CatalogId { get; set; } = string.Empty;
    public string FeedFormat { get; set; } = "JSON"; // "CSV" or "JSON"
    public int TotalProducts { get; set; }
    public DateTime LastSyncedAtUtc { get; set; } = DateTime.UtcNow;
    public string SyncStatus { get; set; } = "Success";
}

public class WhatsAppMpmSection
{
    public string Title { get; set; } = string.Empty;
    public List<string> ProductRetailerIds { get; set; } = new();
}

public class WhatsAppInboundOrderItem
{
    public string ProductRetailerId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal ItemPrice { get; set; }
    public string Currency { get; set; } = "TND";
}
