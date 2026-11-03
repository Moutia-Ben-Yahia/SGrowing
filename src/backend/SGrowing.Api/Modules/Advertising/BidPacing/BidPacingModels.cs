using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.BidPacing;

public enum PacingMode
{
    EvenThroughoutDay,
    AcceleratedHighIntent,
    ThrottledOverSpend
}

public record CampaignPacingRecord(
    string CampaignId,
    string CampaignName,
    string Platform,
    decimal DailyBudgetUsd,
    decimal SpendToDateUsd,
    double BurnRatePercentage,
    double CurrentBidMultiplier,
    PacingMode Mode,
    int TargetAccountImpressions
);

public record AdjustBidPacingRequest(
    string CampaignId,
    double NewBidMultiplier,
    PacingMode NewMode
);

public record PacingAdjustmentResult(
    string CampaignId,
    double UpdatedMultiplier,
    PacingMode Mode,
    string StatusSummary
);
