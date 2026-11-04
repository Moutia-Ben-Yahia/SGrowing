using SGrowing.Api.Modules.Sales.SpiffGamification;
using Xunit;

namespace SGrowing.Tests.Sales;

public sealed class SpiffGamificationTests
{
    private readonly SpiffGamificationEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededCampaignsAndLeaderboard()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveCampaignsCount >= 1);
        Assert.True(overview.TotalEscrowFundedUsd > 50000.0);
        Assert.NotEmpty(overview.ActiveCampaigns);
        Assert.NotEmpty(overview.Leaderboard);
    }

    [Fact]
    public void AwardDealSpiff_ValidDeal_AllocatesInstantEscrow()
    {
        var camp = _engine.CreateCampaign("Sprint Closer Bonus", SpiffRewardType.CashBonus, 3000.0, 30000.0);
        var escrow = _engine.AwardDealSpiff(camp.CampaignId, "DEAL-ENT-440", "Alexandra Hayes");

        Assert.NotNull(escrow);
        Assert.StartsWith("ESC-", escrow.EscrowId);
        Assert.Equal(3000.0, escrow.EscrowAmountUsd);
        Assert.Equal("PAID_INSTANT_ESCROW", escrow.PayoutStatus);
    }

    [Fact]
    public void GetLeaderboard_ReturnsRankedTopPerformers()
    {
        var leaderboard = _engine.GetLeaderboard();

        Assert.NotEmpty(leaderboard);
        Assert.Equal(1, leaderboard[0].Rank);
        Assert.Equal("Alexandra Hayes", leaderboard[0].SalesRepName);
        Assert.True(leaderboard[0].TotalSpiffEarnedUsd >= 20000.0);
    }

    [Fact]
    public void CreateCampaign_InitializesBudgetAndStatusCorrectly()
    {
        var campaign = _engine.CreateCampaign("Mid-Market Surge", SpiffRewardType.LuxuryTravelExperience, 1500.0, 15000.0);

        Assert.NotNull(campaign);
        Assert.StartsWith("SPIFF-CAMP-", campaign.CampaignId);
        Assert.Equal("Mid-Market Surge", campaign.CampaignName);
        Assert.Equal(SpiffRewardType.LuxuryTravelExperience, campaign.RewardType);
        Assert.Equal(1500.0, campaign.RewardAmountPerDeal);
        Assert.Equal(15000.0, campaign.TotalEscrowBudgetUsd);
        Assert.Equal(15000.0, campaign.RemainingEscrowUsd);
        Assert.True(campaign.IsActive);
    }

    [Fact]
    public void AwardDealSpiff_ReducesRemainingEscrowBudget()
    {
        var campaign = _engine.CreateCampaign("Flash Spiff", SpiffRewardType.CashBonus, 1000.0, 5000.0);

        _engine.AwardDealSpiff(campaign.CampaignId, "DEAL-001", "John Doe");

        var overview = _engine.GetOverview();
        var refreshed = overview.ActiveCampaigns.First(c => c.CampaignId == campaign.CampaignId);
        Assert.Equal(4000.0, refreshed.RemainingEscrowUsd);
    }

    [Fact]
    public void AwardDealSpiff_WhenCampaignNotFound_UsesFallbackAmountWithoutCrashing()
    {
        var escrow = _engine.AwardDealSpiff("NON_EXISTENT_CAMP", "DEAL-XYZ", "Jane Doe");

        Assert.NotNull(escrow);
        Assert.Equal(2500.0, escrow.EscrowAmountUsd); // default fallback amount
        Assert.Equal("Jane Doe", escrow.SalesRepName);
    }

    [Fact]
    public void AwardDealSpiff_WhenRemainingBudgetExhausted_ClampsRemainingToZero()
    {
        var campaign = _engine.CreateCampaign("Small Budget Campaign", SpiffRewardType.CashBonus, 2000.0, 3000.0);

        // Award deal 1: remaining becomes 1000
        _engine.AwardDealSpiff(campaign.CampaignId, "DEAL-1", "Rep A");
        // Award deal 2: remaining would be 1000 - 2000 = -1000, but clamped to 0
        _engine.AwardDealSpiff(campaign.CampaignId, "DEAL-2", "Rep B");

        var overview = _engine.GetOverview();
        var refreshed = overview.ActiveCampaigns.First(c => c.CampaignId == campaign.CampaignId);
        Assert.Equal(0.0, refreshed.RemainingEscrowUsd);
    }

    [Fact]
    public void GetOverview_ReportsTotalSpiffsPaidTodayAndRecentEscrows()
    {
        var overview = _engine.GetOverview();

        Assert.Equal(12500.0, overview.TotalSpiffsPaidTodayUsd);
        Assert.NotEmpty(overview.RecentEscrows);
        Assert.All(overview.RecentEscrows, e => Assert.StartsWith("ESC-", e.EscrowId));
    }
}
