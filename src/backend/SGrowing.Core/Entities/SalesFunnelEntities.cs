namespace SGrowing.Core.Entities;

public enum FunnelStepType
{
    LandingPage = 1,
    Checkout = 2,
    OrderBump = 3,
    OneClickUpsell = 4,
    Downsell = 5,
    ThankYouPage = 6
}

public class SalesFunnel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int TotalVisitors { get; set; } = 0;
    public int TotalOrders { get; set; } = 0;
    public decimal TotalRevenue { get; set; } = 0m;
    public string Currency { get; set; } = "TND";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static SalesFunnel Create(
        Guid tenantId,
        string name,
        string slug,
        string description = "",
        string currency = "TND")
    {
        return new SalesFunnel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description.Trim(),
            Currency = currency.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class FunnelStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid FunnelId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public FunnelStepType StepType { get; set; } = FunnelStepType.LandingPage;
    public int StepOrder { get; set; } = 1;
    public string TargetUrlOrSlug { get; set; } = string.Empty;
    public string PageTemplateJson { get; set; } = "{}";
    public int VisitorsCount { get; set; } = 0;
    public int ConversionsCount { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static FunnelStep Create(
        Guid tenantId,
        Guid funnelId,
        string stepName,
        FunnelStepType stepType,
        int stepOrder,
        string slug = "",
        string templateJson = "{}")
    {
        return new FunnelStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FunnelId = funnelId,
            StepName = stepName.Trim(),
            StepType = stepType,
            StepOrder = stepOrder,
            TargetUrlOrSlug = slug.Trim(),
            PageTemplateJson = templateJson,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class OrderBumpOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid FunnelId { get; set; }
    public Guid StepId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; } = 0m;
    public decimal DiscountedPrice { get; set; } = 0m;
    public string BadgeText { get; set; } = "ONE-TIME OFFER - 50% OFF";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static OrderBumpOffer Create(
        Guid tenantId,
        Guid funnelId,
        Guid stepId,
        string title,
        string headline,
        string sku,
        decimal originalPrice,
        decimal discountedPrice,
        string badgeText = "SPECIAL IN-CHECKOUT BUMP")
    {
        return new OrderBumpOffer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FunnelId = funnelId,
            StepId = stepId,
            Title = title.Trim(),
            Headline = headline.Trim(),
            ProductSku = sku.Trim(),
            OriginalPrice = originalPrice,
            DiscountedPrice = discountedPrice,
            BadgeText = badgeText.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class OneClickUpsellOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid FunnelId { get; set; }
    public Guid StepId { get; set; }
    public string OfferTitle { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; } = 0m;
    public decimal UpsellPrice { get; set; } = 0m;
    public decimal SavingsAmount => OriginalPrice - UpsellPrice;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static OneClickUpsellOffer Create(
        Guid tenantId,
        Guid funnelId,
        Guid stepId,
        string title,
        string sku,
        string headline,
        decimal originalPrice,
        decimal upsellPrice,
        string imageUrl = "")
    {
        return new OneClickUpsellOffer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FunnelId = funnelId,
            StepId = stepId,
            OfferTitle = title.Trim(),
            ProductSku = sku.Trim(),
            Headline = headline.Trim(),
            OriginalPrice = originalPrice,
            UpsellPrice = upsellPrice,
            ImageUrl = imageUrl.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public enum FunnelEventType
{
    PageView = 1,
    CheckoutInitiated = 2,
    OrderBumpAccepted = 3,
    UpsellAccepted = 4,
    UpsellDeclined = 5,
    FunnelOrderCompleted = 6
}

public class FunnelConversionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid FunnelId { get; set; }
    public Guid? StepId { get; set; }
    public FunnelEventType EventType { get; set; } = FunnelEventType.PageView;
    public Guid? OrderId { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal EventValue { get; set; } = 0m;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    public static FunnelConversionEvent Create(
        Guid tenantId,
        Guid funnelId,
        FunnelEventType eventType,
        decimal value = 0m,
        Guid? stepId = null,
        Guid? orderId = null,
        string? customerPhone = null)
    {
        return new FunnelConversionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FunnelId = funnelId,
            StepId = stepId,
            EventType = eventType,
            EventValue = value,
            OrderId = orderId,
            CustomerPhone = customerPhone,
            CapturedAtUtc = DateTime.UtcNow
        };
    }
}
