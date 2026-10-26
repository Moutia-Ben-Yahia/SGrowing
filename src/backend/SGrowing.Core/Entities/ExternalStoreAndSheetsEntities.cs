namespace SGrowing.Core.Entities;

public enum ExternalStorePlatform
{
    Shopify = 1,
    WooCommerce = 2,
    CustomWebhook = 3
}

public enum StoreSyncDirection
{
    InboundOrderCreated = 1,
    OutboundFulfillmentPushed = 2
}

public class ExternalStoreSyncRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public ExternalStorePlatform Platform { get; set; } = ExternalStorePlatform.Shopify;
    public string ExternalOrderId { get; set; } = string.Empty;
    public string ExternalOrderNumber { get; set; } = string.Empty;
    public Guid SGrowingOrderId { get; set; }
    public StoreSyncDirection SyncDirection { get; set; }
    public string Status { get; set; } = "Success"; // Success, Failed
    public string PayloadJson { get; set; } = "{}";
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}

public class GoogleSheetsSyncConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetTabName { get; set; } = "Orders";
    public string WebhookOrEndpointUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool AutoSyncOnConfirmed { get; set; } = true;
    public DateTime? LastSyncedAtUtc { get; set; }
    public int RowsAppendedCount { get; set; } = 0;
}
