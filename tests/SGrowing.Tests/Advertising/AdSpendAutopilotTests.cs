using SGrowing.Api.Modules.Advertising.SpendAutopilot;
using Xunit;

namespace SGrowing.Tests.Advertising;

public sealed class AdSpendAutopilotTests
{
    private readonly AdSpendAutopilotEngine _engine = new();

    [Fact]
    public async Task GetCampaigns_ReturnsMultiChannelCampaignsWithRoas()
    {
        var campaigns = await _engine.GetCampaignsAsync();

        Assert.NotNull(campaigns);
        Assert.NotEmpty(campaigns);
        Assert.Contains(campaigns, c => c.Channel == "GoogleSearch");
        Assert.Contains(campaigns, c => c.Status == "AnomalyPaused");
    }

    [Fact]
    public async Task RebalanceBudget_ShiftsBudgetBetweenCampaigns()
    {
        var request = new RebalanceBudgetRequest(
            SourceCampaignId: "cmp_meta_retargeting",
            TargetCampaignId: "cmp_google_pmax",
            Amount: 500m
        );

        var updatedTarget = await _engine.RebalanceBudgetAsync(request);

        Assert.NotNull(updatedTarget);
        Assert.Equal("cmp_google_pmax", updatedTarget.CampaignId);
        Assert.Equal(4000m, updatedTarget.DailyBudget); // 3500 + 500

        var campaigns = await _engine.GetCampaignsAsync();
        var source = campaigns.First(c => c.CampaignId == "cmp_meta_retargeting");
        Assert.Equal(1700m, source.DailyBudget); // 2200 - 500
    }

    [Fact]
    public async Task ExecuteBidShading_ReducesBidCostWhilePreservingWinRate()
    {
        var rawBid = 5.00m;
        var telemetry = await _engine.ExecuteBidShadingAsync("cmp_google_pmax", rawBid);

        Assert.NotNull(telemetry);
        Assert.True(telemetry.ShadedBidPrice < rawBid);
        Assert.True(telemetry.SavingsAmount > 0);
        Assert.True(telemetry.AuctionWinRatePercent >= 80.0);
    }
}
