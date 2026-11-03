using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Analytics.ShapleyAttribution;

public enum TouchpointType
{
    PaidSearchAd,
    OrganicSeoArticle,
    TechnicalWhitepaperRead,
    OutboundSdrColdEmail,
    WebinarAttendance,
    ExecutiveDemoMeeting,
    RetargetingSocialAd
}

public record TouchpointEvent(
    string EventId,
    TouchpointType Channel,
    DateTime OccurredAtUtc,
    int EngagementSeconds,
    decimal EstimatedSpendCost
);

public record ChannelAttributionScore(
    TouchpointType Channel,
    double FirstTouchPercentage,
    double LastTouchPercentage,
    double LinearPercentage,
    double ShapleyFairValuePercentage,
    decimal AttributedRevenueAmount,
    string RecommendedBudgetAction // "Increase", "Maintain", "Trim"
);

public record ShapleyAttributionReport(
    string DealId,
    string CustomerName,
    decimal ClosedDealValue,
    List<TouchpointEvent> JourneyEvents,
    List<ChannelAttributionScore> ChannelScores,
    string KeyTakeaway
);

public record CalculateShapleyRequest(
    string DealId,
    string CustomerName,
    decimal ClosedDealValue,
    List<TouchpointEvent>? Events
);
