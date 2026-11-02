namespace SGrowing.Api.Modules.Advertising.SpendAutopilot;

public record AdCampaignChannel(
    string CampaignId,
    string CampaignName,
    string Channel,
    decimal DailyBudget,
    decimal CurrentSpend,
    decimal TargetCpa,
    decimal CurrentCpa,
    double Roas,
    string Status
);

public record BidShadingTelemetry(
    string CampaignId,
    decimal RawWinningBid,
    decimal ShadedBidPrice,
    decimal SavingsAmount,
    double AuctionWinRatePercent,
    DateTime TimestampUtc
);

public record BudgetRebalanceRecommendation(
    string SourceCampaignId,
    string TargetCampaignId,
    decimal ReallocatedAmount,
    double ExpectedRoasLiftPercent,
    string Reasoning
);

public record RebalanceBudgetRequest(
    string SourceCampaignId,
    string TargetCampaignId,
    decimal Amount
);

public record AutopilotEngineSummary(
    decimal TotalDailyBudget,
    decimal TotalCurrentSpend,
    decimal TotalSavingsFromBidShading,
    double BlendedRoas,
    int TotalActiveCampaigns,
    int AnomalyPausedCount
);
