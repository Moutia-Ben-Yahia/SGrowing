namespace SGrowing.Core.Entities;

public enum AdPlatformType
{
    Meta,
    TikTok,
    Google,
    Snapchat
}

public enum AdOptimizationAction
{
    NoAction,
    PauseAdSet,
    ScaleBudget,
    AlertMediaBuyer
}

public class CampaignAdSpendRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public AdPlatformType Platform { get; set; } = AdPlatformType.Meta;
    public string CampaignId { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public string AdSetId { get; set; } = string.Empty;
    public string AdSetName { get; set; } = string.Empty;
    public decimal SpendAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime DateUtc { get; set; } = DateTime.UtcNow.Date;
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public int ReportedPixelPurchases { get; set; }
    public decimal ReportedPixelRevenue { get; set; }
    public DateTime IngestedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AdSetDeliveredRoasMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public AdPlatformType Platform { get; set; } = AdPlatformType.Meta;
    public string AdSetId { get; set; } = string.Empty;
    public string AdSetName { get; set; } = string.Empty;
    public DateTime DateUtc { get; set; } = DateTime.UtcNow.Date;
    public decimal TotalAdSpend { get; set; }
    public int TotalOrdersAttributed { get; set; }
    public int DeliveredOrdersCount { get; set; }
    public int ReturnedOrdersCount { get; set; }
    public decimal DeliveryRatePercentage { get; set; }
    public decimal GrossAttributedGmv { get; set; }
    public decimal DeliveredNetCashRemitted { get; set; }
    public decimal TotalCogs { get; set; }
    public decimal TotalCourierShippingFees { get; set; }
    public decimal NetContributionMargin { get; set; } // DeliveredNetCash - TotalCogs - CourierFees
    public decimal RealDeliveredRoas { get; set; } // DeliveredNetCash / TotalAdSpend
    public decimal StandardPixelRoas { get; set; } // GrossAttributedGmv / TotalAdSpend
    public decimal RoasLeakagePercentage { get; set; } // (StandardPixelRoas - RealDeliveredRoas) / StandardPixelRoas * 100
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;

    public static AdSetDeliveredRoasMetric Calculate(
        Guid tenantId,
        AdPlatformType platform,
        string adSetId,
        string adSetName,
        DateTime date,
        decimal adSpend,
        int totalOrders,
        int deliveredOrders,
        int returnedOrders,
        decimal grossGmv,
        decimal deliveredNetCash,
        decimal cogs,
        decimal courierFees)
    {
        decimal deliveryRate = totalOrders > 0
            ? Math.Round((decimal)deliveredOrders / totalOrders * 100m, 1)
            : 0m;

        decimal netMargin = deliveredNetCash - cogs - courierFees;
        decimal realRoas = adSpend > 0 ? Math.Round(deliveredNetCash / adSpend, 2) : 0m;
        decimal pixelRoas = adSpend > 0 ? Math.Round(grossGmv / adSpend, 2) : 0m;

        decimal leakage = pixelRoas > 0
            ? Math.Round((pixelRoas - realRoas) / pixelRoas * 100m, 1)
            : 0m;

        return new AdSetDeliveredRoasMetric
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Platform = platform,
            AdSetId = adSetId.Trim(),
            AdSetName = adSetName.Trim(),
            DateUtc = date.Date,
            TotalAdSpend = adSpend,
            TotalOrdersAttributed = totalOrders,
            DeliveredOrdersCount = deliveredOrders,
            ReturnedOrdersCount = returnedOrders,
            DeliveryRatePercentage = deliveryRate,
            GrossAttributedGmv = grossGmv,
            DeliveredNetCashRemitted = deliveredNetCash,
            TotalCogs = cogs,
            TotalCourierShippingFees = courierFees,
            NetContributionMargin = netMargin,
            RealDeliveredRoas = realRoas,
            StandardPixelRoas = pixelRoas,
            RoasLeakagePercentage = leakage,
            CalculatedAtUtc = DateTime.UtcNow
        };
    }
}

public class AdOptimizationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public AdPlatformType Platform { get; set; } = AdPlatformType.Meta;
    public string RuleName { get; set; } = string.Empty;
    public decimal MinSpendThreshold { get; set; } = 50.0m;
    public decimal MinDeliveryRateCutoff { get; set; } = 55.0m; // If < 55% delivery -> killswitch
    public decimal MinDeliveredRoasToScale { get; set; } = 3.5m; // If >= 3.5x real ROAS and high delivery -> scale
    public decimal DailyBudgetScaleStepPercentage { get; set; } = 15.0m;
    public bool IsAutoExecuteEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AdPlatformExecutionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? RuleId { get; set; }
    public AdPlatformType Platform { get; set; } = AdPlatformType.Meta;
    public string AdSetId { get; set; } = string.Empty;
    public AdOptimizationAction Action { get; set; } = AdOptimizationAction.NoAction;
    public string Reason { get; set; } = string.Empty;
    public decimal CurrentDailyBudget { get; set; }
    public decimal? NewDailyBudget { get; set; }
    public bool ExecutionSuccess { get; set; } = true;
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}
