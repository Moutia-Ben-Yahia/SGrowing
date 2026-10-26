namespace SGrowing.Core.Entities;

public class PostPurchaseSurvey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int? Rating { get; set; } // 1 to 5
    public string? FeedbackNotes { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
    public bool IsPromoter => Rating.HasValue && Rating.Value >= 4;
    public string? DiscountCodeIssued { get; set; }
}

public class ProductBurnRateConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int EstimatedDaysOfUse { get; set; } = 45; // e.g. 50ml fragrance = 45 days
    public int ReminderLeadDays { get; set; } = 10;   // trigger at day 35
    public decimal IncentiveDiscountPercent { get; set; } = 15.0m;
    public bool IsActive { get; set; } = true;
}

public class ReplenishmentSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime DeliveredAtUtc { get; set; }
    public DateTime ScheduledReminderUtc { get; set; }
    public bool IsTriggered { get; set; } = false;
    public DateTime? TriggeredAtUtc { get; set; }
    public DateTime? ReorderedAtUtc { get; set; }
    public string? DiscountCode { get; set; }
}

public record ReplenishmentTriggerResult(
    string CustomerPhone,
    string CustomerName,
    string ProductName,
    string MessageText,
    string DiscountCode,
    string WhatsAppCardJson);
