namespace SGrowing.Core.Entities;

public enum TouchpointStage
{
    FirstDiscovery = 1,
    LeadCreated = 2,
    OpportunityCreated = 3,
    DealWon = 4
}

public enum AttributionModelType
{
    FirstTouch = 1,
    LastTouch = 2,
    Linear = 3,
    WShaped = 4,
    TimeDecay = 5
}

public class AttributionTouchpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public Guid? DealId { get; set; }
    public string Channel { get; set; } = string.Empty; // "GoogleAds", "MetaAds", "LinkedIn", "WhatsApp", "Email", "Direct"
    public string CampaignName { get; set; } = string.Empty;
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public TouchpointStage Stage { get; set; } = TouchpointStage.FirstDiscovery;
    public decimal Cost { get; set; } = 0;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public static AttributionTouchpoint Create(
        Guid tenantId,
        Guid contactId,
        string channel,
        string campaignName,
        TouchpointStage stage,
        Guid? dealId = null,
        decimal cost = 0,
        string? utmSource = null,
        string? utmMedium = null,
        string? utmCampaign = null)
    {
        return new AttributionTouchpoint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            DealId = dealId,
            Channel = channel.Trim(),
            CampaignName = campaignName.Trim(),
            Stage = stage,
            Cost = cost,
            UtmSource = utmSource?.Trim(),
            UtmMedium = utmMedium?.Trim(),
            UtmCampaign = utmCampaign?.Trim(),
            OccurredAtUtc = DateTime.UtcNow
        };
    }
}

public class AttributedRevenueItem
{
    public Guid TouchpointId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public TouchpointStage Stage { get; set; }
    public decimal AttributedRevenue { get; set; }
    public decimal WeightPercent { get; set; }
}

public class ExecutiveKpiSummary
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalAdSpend { get; set; }
    public decimal ReturnOnAdSpend { get; set; } // ROAS (e.g. 4.5x)
    public decimal CustomerAcquisitionCost { get; set; } // CAC
    public decimal AverageDealSize { get; set; }
    public int ClosedWonDealsCount { get; set; }
}
