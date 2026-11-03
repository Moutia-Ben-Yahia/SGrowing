using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Marketing.AdSpendRedistribution;

public record AttributionChannelMetric(
    string ChannelId,
    string ChannelName,
    decimal CurrentSpend,
    decimal FirstTouchSharePct,
    decimal LastTouchSharePct,
    decimal ShapleySharePct,
    decimal AttributionSkewPct,
    decimal RecommendedSpendShift,
    decimal ProjectedRoasLift
);

public record TouchpointJourney(
    string JourneyId,
    string AccountName,
    decimal DealArr,
    List<string> TouchpointChannels,
    DateTime ClosedAt
);

public record AdSpendRedistributionOverview(
    decimal TotalPipelineArr,
    decimal TotalMarketingSpend,
    double OverallBlendedRoas,
    List<AttributionChannelMetric> Channels,
    List<TouchpointJourney> RecentJourneys,
    decimal ProjectedArrUplift
);

public record ReallocateBudgetCommand(
    string ChannelId,
    decimal DeltaAmount,
    string RebalanceMode
);

public record ReallocationResult(
    string ChannelId,
    decimal PreviousSpend,
    decimal NewSpend,
    decimal ExpectedRoas,
    string ExecutionLog
);
