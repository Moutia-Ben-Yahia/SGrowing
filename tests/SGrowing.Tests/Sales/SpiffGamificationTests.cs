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
}
