namespace SGrowing.Core.Entities;

public enum UgcHookAngle
{
    ProblemAgitator = 1,
    CompetitorCallout = 2,
    CulturalCodReassurance = 3,
    UnboxingDemonstration = 4,
    ScarcityFlashDeal = 5
}

public class UgcVideoScript
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Language { get; set; } = "Darija"; // Darija, French, Arabic
    public string TargetAudience { get; set; } = "Shoppers 18-35";
    public UgcHookAngle HookAngle { get; set; } = UgcHookAngle.ProblemAgitator;
    public string HookText { get; set; } = string.Empty;
    public string ScenesJson { get; set; } = "[]"; // List of UgcScriptScene
    public string CallToAction { get; set; } = string.Empty;
    public int EstimatedDurationSeconds { get; set; } = 35;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static UgcVideoScript Create(
        Guid tenantId,
        string sku,
        string productName,
        string language,
        string audience,
        UgcHookAngle hookAngle,
        string hookText,
        List<UgcScriptScene> scenes,
        string callToAction,
        int durationSeconds = 35)
    {
        return new UgcVideoScript
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductSku = sku.Trim(),
            ProductName = productName.Trim(),
            Language = language.Trim(),
            TargetAudience = audience.Trim(),
            HookAngle = hookAngle,
            HookText = hookText.Trim(),
            ScenesJson = System.Text.Json.JsonSerializer.Serialize(scenes),
            CallToAction = callToAction.Trim(),
            EstimatedDurationSeconds = durationSeconds,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public record UgcScriptScene(
    int SceneNumber,
    string VisualCue,
    string CreatorAction,
    string VoiceoverText,
    string OnscreenText,
    int DurationSeconds
);

public class CompetitorAdRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public string FanPageId { get; set; } = string.Empty;
    public string Platform { get; set; } = "Meta"; // Meta, TikTok, Snapchat
    public string AdCreativeId { get; set; } = string.Empty;
    public string HookSnippet { get; set; } = string.Empty;
    public string OfferDetails { get; set; } = string.Empty;
    public decimal EstimatedDailySpend { get; set; }
    public string ThreatLevel { get; set; } = "Medium"; // Low, Medium, High
    public bool IsActive { get; set; } = true;
    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public static CompetitorAdRecord Create(
        Guid tenantId,
        string competitorName,
        string platform,
        string hookSnippet,
        string offerDetails,
        decimal estimatedDailySpend,
        string threatLevel = "Medium")
    {
        return new CompetitorAdRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompetitorName = competitorName.Trim(),
            Platform = platform.Trim(),
            AdCreativeId = $"ad_{Guid.NewGuid():N}",
            HookSnippet = hookSnippet.Trim(),
            OfferDetails = offerDetails.Trim(),
            EstimatedDailySpend = estimatedDailySpend,
            ThreatLevel = threatLevel.Trim(),
            IsActive = true,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };
    }
}

public class CompetitorPriceTracker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TargetSku { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public string CompetitorUrl { get; set; } = string.Empty;
    public decimal CompetitorPrice { get; set; }
    public decimal OurPrice { get; set; }
    public decimal PriceDifferencePercentage { get; set; }
    public string RecommendedCounterAction { get; set; } = "MaintainPrice"; // MaintainPrice, FlashDiscount, BundleIncentive, UrgencyBanner
    public DateTime LastCheckedAtUtc { get; set; } = DateTime.UtcNow;

    public static CompetitorPriceTracker Create(
        Guid tenantId,
        string sku,
        string competitorName,
        string url,
        decimal competitorPrice,
        decimal ourPrice)
    {
        string counterAction = "MaintainPrice";
        if (competitorPrice < ourPrice * 0.85m)
        {
            counterAction = "BundleIncentive"; // Competitor severely undercutting: bundle gifts rather than price war
        }
        else if (competitorPrice < ourPrice)
        {
            counterAction = "FlashDiscount";
        }

        var diff = ourPrice > 0 ? Math.Round(((competitorPrice - ourPrice) / ourPrice) * 100m, 1) : 0;

        return new CompetitorPriceTracker
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetSku = sku.Trim(),
            CompetitorName = competitorName.Trim(),
            CompetitorUrl = url.Trim(),
            CompetitorPrice = competitorPrice,
            OurPrice = ourPrice,
            PriceDifferencePercentage = diff,
            RecommendedCounterAction = counterAction,
            LastCheckedAtUtc = DateTime.UtcNow
        };
    }
}
