using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.RetargetingMatrix;

public enum RetargetingStage
{
    PricingPageEvaluator,
    DemoAttendee,
    StalledProposal,
    ClosedWonCustomer,
    SuppressedNegativeIntent
}

public record ImpressionDecision(
    string AccountDomain,
    bool ShouldServeImpression,
    string SelectedCreativeTheme,
    string RecommendedChannel,
    int DailyImpressionsServed,
    int DailyFrequencyCap,
    string Reason
);

public record BuyerRetargetingProfile(
    string AccountDomain,
    string CompanyName,
    RetargetingStage Stage,
    int ImpressionsLast24h,
    int MaxDailyCap,
    DateTime LastImpressionUtc,
    bool IsSuppressed
);

public record EvaluateImpressionRequest(
    string AccountDomain,
    string Channel // "LinkedIn", "Meta", "GoogleDSP"
);

public record UpdateBuyerStageRequest(
    string AccountDomain,
    RetargetingStage NewStage
);
