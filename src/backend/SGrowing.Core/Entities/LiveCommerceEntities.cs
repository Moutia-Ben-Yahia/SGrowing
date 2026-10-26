namespace SGrowing.Core.Entities;

public enum LiveStreamPlatform
{
    TikTokLive = 1,
    FacebookLive = 2,
    InstagramLive = 3
}

public class LiveStreamSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public LiveStreamPlatform Platform { get; set; } = LiveStreamPlatform.TikTokLive;
    public string Title { get; set; } = string.Empty;
    public string StreamExternalId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int TotalCommentsProcessed { get; set; }
    public int TotalOrdersGenerated { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }

    public static LiveStreamSession Create(Guid tenantId, string title, LiveStreamPlatform platform, string streamId)
    {
        return new LiveStreamSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Platform = platform,
            StreamExternalId = streamId.Trim(),
            IsActive = true,
            StartedAtUtc = DateTime.UtcNow
        };
    }
}

public class LiveFlashDeal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string TriggerKeyword { get; set; } = "ACHETER"; // e.g. "ACHETER", "PROMO"
    public decimal FlashPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public int MaxQuantity { get; set; } = 20;
    public int SoldQuantity { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public int RemainingQuantity => Math.Max(0, MaxQuantity - SoldQuantity);
}

public record LiveOverlayStockDto(
    string ProductName,
    string Sku,
    decimal FlashPrice,
    int RemainingUnits,
    string TriggerKeyword,
    bool IsUrgentAlert);

public record LiveCommentOrderResult(
    bool Success,
    Guid? OrderId,
    string CustomerUsername,
    string Sku,
    decimal Price,
    string Message,
    string CheckoutCardJson);
