namespace SGrowing.Core.Entities;

public enum AdPlatform
{
    MetaCapi = 1,
    TikTokEvents = 2,
    GoogleAdsOffline = 3
}

public enum ConversionEventType
{
    Lead = 1,
    AddToCart = 2,
    InitiateCheckout = 3,
    Purchase = 4,
    CompleteRegistration = 5
}

public enum ConversionDispatchStatus
{
    Pending = 1,
    Dispatched = 2,
    Failed = 3
}

public class ServerConversionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public AdPlatform Platform { get; set; } = AdPlatform.MetaCapi;
    public ConversionEventType EventType { get; set; } = ConversionEventType.Purchase;
    public string EventId { get; set; } = string.Empty; // Unique deduplication ID
    public DateTime EventTimeUtc { get; set; } = DateTime.UtcNow;
    public string? HashedEmail { get; set; }
    public string? HashedPhone { get; set; }
    public string? HashedFirstName { get; set; }
    public string? HashedLastName { get; set; }
    public string? HashedCity { get; set; }
    public string? HashedCountry { get; set; }
    public string? Fbc { get; set; } // Facebook Click ID (e.g., fb.1.1554772599.IwAR...)
    public string? Fbp { get; set; } // Facebook Browser Cookie ID (e.g., fb.1.1558571054389.1098115397)
    public string? ClientUserAgent { get; set; }
    public string? ClientIpAddress { get; set; }
    public decimal ValueAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string CustomDataJson { get; set; } = "{}";
    public ConversionDispatchStatus Status { get; set; } = ConversionDispatchStatus.Pending;
    public DateTime? DispatchedAtUtc { get; set; }
    public string? ApiResponseJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ConversionTrackingConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public AdPlatform Platform { get; set; } = AdPlatform.MetaCapi;
    public string PixelOrDatasetId { get; set; } = string.Empty;
    public string ApiAccessToken { get; set; } = string.Empty;
    public string? TestEventCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
