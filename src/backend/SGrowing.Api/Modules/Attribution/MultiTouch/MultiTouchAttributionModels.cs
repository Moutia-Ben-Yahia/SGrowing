namespace SGrowing.Api.Modules.Attribution.MultiTouch;

public enum AttributionModelType
{
    FirstTouch,
    LastTouch,
    Linear,
    TimeDecay,
    WShaped
}

public enum TouchpointChannel
{
    OrganicSearch,
    MetaAds,
    GoogleAds,
    LinkedInAds,
    EmailNurture,
    OutboundSdr,
    Direct
}

public enum TouchpointStage
{
    FirstDiscovery,
    LeadCreation,
    OpportunityCreation,
    DealClose,
    MidFunnel
}

public record CustomerTouchpoint(
    Guid Id,
    string TenantId,
    string CustomerId,
    TouchpointChannel Channel,
    string CampaignId,
    TouchpointStage Stage,
    decimal TouchCost,
    DateTime TimestampUtc
);

public record AttributionCalculationRequest(
    string TenantId,
    string CustomerId,
    decimal DealRevenue,
    AttributionModelType Model
);

public record AttributedChannelShare(
    TouchpointChannel Channel,
    double WeightPercent,
    decimal AttributedRevenue,
    decimal TotalCost,
    decimal Roas
);

public record AttributionResult(
    string CustomerId,
    AttributionModelType Model,
    decimal TotalRevenue,
    List<AttributedChannelShare> ChannelShares,
    DateTime CalculatedAtUtc
);

public record PredictLtvRequest(
    string TenantId,
    string CustomerId,
    decimal HistoricalRevenue,
    int CompletedTransactions,
    DateTime FirstSeenUtc
);

public record PredictiveLtvResult(
    string CustomerId,
    decimal Predicted12MonthLtv,
    decimal Predicted36MonthLtv,
    double ChurnProbabilityPercent,
    string RetentionRiskTier
);
