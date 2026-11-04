using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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
        Assert.True(campaigns.First().Roas >= campaigns.Last().Roas); // Descending order
    }

    [Fact]
    public async Task GetRebalanceRecommendations_IdentifiesHighAndLowPerformers()
    {
        var recommendations = await _engine.GetRebalanceRecommendationsAsync();

        Assert.NotNull(recommendations);
        Assert.Single(recommendations);
        var rec = recommendations[0];
        Assert.Equal("cmp_meta_retargeting", rec.SourceCampaignId);
        Assert.Equal("cmp_google_pmax", rec.TargetCampaignId);
        Assert.True(rec.ReallocatedAmount > 0);
        Assert.True(rec.ExpectedRoasLiftPercent > 10.0);
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
        Assert.Equal("Optimized", updatedTarget.Status);

        var campaigns = await _engine.GetCampaignsAsync();
        var source = campaigns.First(c => c.CampaignId == "cmp_meta_retargeting");
        Assert.Equal(1700m, source.DailyBudget); // 2200 - 500
    }

    [Fact]
    public async Task RebalanceBudget_FloorsSourceBudgetAtMinimumOneHundred()
    {
        var request = new RebalanceBudgetRequest(
            SourceCampaignId: "cmp_meta_retargeting",
            TargetCampaignId: "cmp_google_pmax",
            Amount: 50000m // massive amount exceeding current budget of 2200
        );

        var updatedTarget = await _engine.RebalanceBudgetAsync(request);

        Assert.NotNull(updatedTarget);
        var campaigns = await _engine.GetCampaignsAsync();
        var source = campaigns.First(c => c.CampaignId == "cmp_meta_retargeting");
        Assert.Equal(100m, source.DailyBudget); // Clamped at 100m floor
    }

    [Fact]
    public async Task RebalanceBudget_ThrowsWhenCampaignNotFound()
    {
        var requestMissingSource = new RebalanceBudgetRequest("missing_src", "cmp_google_pmax", 200m);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _engine.RebalanceBudgetAsync(requestMissingSource));

        var requestMissingTarget = new RebalanceBudgetRequest("cmp_meta_retargeting", "missing_tgt", 200m);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _engine.RebalanceBudgetAsync(requestMissingTarget));
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

    [Fact]
    public async Task ExecuteBidShading_ThrowsWhenCampaignNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _engine.ExecuteBidShadingAsync("non_existent_cmp", 4.00m));
    }

    [Fact]
    public async Task GetSummary_CalculatesAggregatedMetricsAccurately()
    {
        var summary = await _engine.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.True(summary.TotalDailyBudget > 0m);
        Assert.True(summary.TotalCurrentSpend > 0m);
        Assert.True(summary.TotalSavingsFromBidShading > 0m);
        Assert.True(summary.BlendedRoas > 0);
        Assert.Equal(2, summary.TotalActiveCampaigns);
        Assert.Equal(1, summary.AnomalyPausedCount);
    }

    [Fact]
    public async Task RebalanceBudget_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _engine.RebalanceBudgetAsync(null!));
    }

    [Fact]
    public async Task ExecuteBidShading_MultipleInvocations_AccumulatesSavingsInSummary()
    {
        var initialSummary = await _engine.GetSummaryAsync();

        // Perform multiple bid shadings on active campaigns
        var t1 = await _engine.ExecuteBidShadingAsync("cmp_google_pmax", 10.00m);
        var t2 = await _engine.ExecuteBidShadingAsync("cmp_meta_retargeting", 8.00m);

        t1.SavingsAmount.Should().Be(1.60m); // 10.00 - 8.40
        t2.SavingsAmount.Should().Be(1.28m); // 8.00 - 6.72

        var updatedSummary = await _engine.GetSummaryAsync();
        updatedSummary.TotalSavingsFromBidShading.Should().BeGreaterThan(initialSummary.TotalSavingsFromBidShading);
    }

    [Fact]
    public async Task RebalanceBudget_ZeroAmount_MaintainsExistingBudgets()
    {
        var beforeCampaigns = await _engine.GetCampaignsAsync();
        var initialSourceBudget = beforeCampaigns.First(c => c.CampaignId == "cmp_meta_retargeting").DailyBudget;
        var initialTargetBudget = beforeCampaigns.First(c => c.CampaignId == "cmp_google_pmax").DailyBudget;

        var request = new RebalanceBudgetRequest("cmp_meta_retargeting", "cmp_google_pmax", 0m);
        var updatedTarget = await _engine.RebalanceBudgetAsync(request);

        updatedTarget.DailyBudget.Should().Be(initialTargetBudget);
        updatedTarget.Status.Should().Be("Optimized");

        var afterCampaigns = await _engine.GetCampaignsAsync();
        var afterSource = afterCampaigns.First(c => c.CampaignId == "cmp_meta_retargeting");
        afterSource.DailyBudget.Should().Be(initialSourceBudget);
    }

    [Fact]
    public async Task GetRebalanceRecommendations_ExcludesAnomalyPausedCampaignsFromSource()
    {
        var recommendations = await _engine.GetRebalanceRecommendationsAsync();

        recommendations.Should().NotBeNull();
        foreach (var rec in recommendations)
        {
            // Anomaly paused campaigns should not be selected as donor source
            rec.SourceCampaignId.Should().NotBe("cmp_tiktok_topview");
            rec.Reasoning.Should().NotBeNullOrWhiteSpace();
            rec.ExpectedRoasLiftPercent.Should().BeGreaterThan(0.0);
        }
    }

    [Fact]
    public async Task ExecuteBidShading_Calculates16PercentDiscountWithExactRounding()
    {
        const decimal rawBid = 100.00m;
        var telemetry = await _engine.ExecuteBidShadingAsync("cmp_google_pmax", rawBid);

        telemetry.ShadedBidPrice.Should().Be(84.00m); // 100 * 0.84
        telemetry.SavingsAmount.Should().Be(16.00m);
        telemetry.AuctionWinRatePercent.Should().Be(84.5);
        telemetry.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetCampaigns_MaintainsStrictRoasDescendingOrder()
    {
        var campaigns = await _engine.GetCampaignsAsync();

        campaigns.Count.Should().BeGreaterThanOrEqualTo(3);
        for (var i = 0; i < campaigns.Count - 1; i++)
        {
            campaigns[i].Roas.Should().BeGreaterThanOrEqualTo(campaigns[i + 1].Roas);
        }
    }
}
