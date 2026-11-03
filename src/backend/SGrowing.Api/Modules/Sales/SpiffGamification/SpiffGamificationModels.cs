namespace SGrowing.Api.Modules.Sales.SpiffGamification;

public enum SpiffRewardType
{
    CashBonus,
    EquityOptionsGrant,
    LuxuryTravelExperience,
    PresidentClubPoints
}

public sealed record SpiffCampaign(
    string CampaignId,
    string CampaignName,
    SpiffRewardType RewardType,
    double RewardAmountPerDeal,
    double TotalEscrowBudgetUsd,
    double RemainingEscrowUsd,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive
);

public sealed record SpiffDealEscrow(
    string EscrowId,
    string CampaignId,
    string DealId,
    string SalesRepName,
    double EscrowAmountUsd,
    DateTime CreatedAtUtc,
    string PayoutStatus
);

public sealed record LeaderboardEntry(
    string RepId,
    string SalesRepName,
    int DealsClosedThisMonth,
    double TotalSpiffEarnedUsd,
    string TierBadge,
    int Rank
);

public sealed record SpiffOverview(
    int ActiveCampaignsCount,
    double TotalEscrowFundedUsd,
    double TotalSpiffsPaidTodayUsd,
    int TopPerformingRepsCount,
    IReadOnlyList<SpiffCampaign> ActiveCampaigns,
    IReadOnlyList<SpiffDealEscrow> RecentEscrows,
    IReadOnlyList<LeaderboardEntry> Leaderboard
);
