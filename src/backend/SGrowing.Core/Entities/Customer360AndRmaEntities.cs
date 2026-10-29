namespace SGrowing.Core.Entities;

public enum CustomerLifecycleStage
{
    Lead = 1,
    Prospect = 2,
    Customer = 3,
    Vip = 4,
    ChurnRisk = 5,
    Blacklisted = 6
}

public enum CustomerChannelIdentityType
{
    Email = 1,
    PhoneE164 = 2,
    WhatsAppId = 3,
    InstagramHandle = 4,
    FacebookPsid = 5,
    TelegramId = 6,
    WebCookieId = 7,
    TikTokHandle = 8
}

public enum RmaStatus
{
    Requested = 1,
    Approved = 2,
    Rejected = 3,
    ReverseCourierDispatched = 4,
    ItemReceived = 5,
    RefundIssued = 6,
    ExchangeShipped = 7
}

public enum RmaReason
{
    Defective = 1,
    WrongItemSent = 2,
    ChangedMind = 3,
    SizeExchange = 4,
    DamagedInTransit = 5
}

public class UnifiedCustomerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string PrimaryName { get; set; } = string.Empty;
    public string? PrimaryEmail { get; set; }
    public string PrimaryPhone { get; set; } = string.Empty; // E.164 formatted
    public string? Company { get; set; }
    public CustomerLifecycleStage LifecycleStage { get; set; } = CustomerLifecycleStage.Lead;
    public int TotalOrdersCount { get; set; } = 0;
    public decimal TotalSpent { get; set; } = 0;
    public double DeliverySuccessRate { get; set; } = 100.0; // %
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}

public class CustomerIdentityLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UnifiedCustomerId { get; set; }
    public CustomerChannelIdentityType IdentityType { get; set; }
    public string IdentityValue { get; set; } = string.Empty;
    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CustomerTimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UnifiedCustomerId { get; set; }
    public string EventType { get; set; } = string.Empty; // OrderPlaced, CallConfirmed, WhatsAppMessage, etc.
    public string Summary { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public class ReturnMerchandiseAuthorization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RmaNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public RmaStatus Status { get; set; } = RmaStatus.Requested;
    public RmaReason Reason { get; set; } = RmaReason.Defective;
    public string? CustomerNotes { get; set; }
    public string PhotoEvidenceUrlsJson { get; set; } = "[]";
    public string? ResolutionNotes { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}
