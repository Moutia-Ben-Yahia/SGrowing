using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Advertising.BidPacing;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class BidPacingTests
{
    private readonly BidPacingEngine _engine = new();

    [Fact]
    public void GetCampaigns_ReturnsSeededCampaignPacingRecords()
    {
        var campaigns = _engine.GetCampaigns();

        campaigns.Should().NotBeNull();
        campaigns.Should().HaveCount(3);
        campaigns.Should().Contain(c => c.CampaignId == "camp_abm_enterprise_01" && c.Mode == PacingMode.AcceleratedHighIntent);
        campaigns.Should().Contain(c => c.CampaignId == "camp_google_high_cpc_02" && c.Mode == PacingMode.ThrottledOverSpend);
    }

    [Fact]
    public void GetCampaigns_Should_Return_Campaigns_Sorted_By_CampaignId()
    {
        var campaigns = _engine.GetCampaigns();

        campaigns.Should().BeInAscendingOrder(c => c.CampaignId);
    }

    [Fact]
    public void AdjustPacing_UpdatesBidMultiplierAndMode()
    {
        var campaignId = "camp_google_high_cpc_02";
        var request = new AdjustBidPacingRequest(campaignId, 1.10, PacingMode.EvenThroughoutDay);

        var result = _engine.AdjustPacing(request);

        result.Should().NotBeNull();
        result.UpdatedMultiplier.Should().Be(1.10);
        result.Mode.Should().Be(PacingMode.EvenThroughoutDay);

        var campaign = _engine.GetCampaigns().Find(c => c.CampaignId == campaignId)!;
        campaign.CurrentBidMultiplier.Should().Be(1.10);
        campaign.Mode.Should().Be(PacingMode.EvenThroughoutDay);
    }

    [Fact]
    public void AdjustPacing_Should_Be_Case_Insensitive_For_CampaignId()
    {
        var campaignId = "CAMP_ABM_ENTERPRISE_01";
        var request = new AdjustBidPacingRequest(campaignId, 1.45, PacingMode.AcceleratedHighIntent);

        var result = _engine.AdjustPacing(request);

        result.Should().NotBeNull();
        result.UpdatedMultiplier.Should().Be(1.45);
        result.Mode.Should().Be(PacingMode.AcceleratedHighIntent);
    }

    [Fact]
    public void AdjustPacing_ThrowsWhenCampaignNotFound()
    {
        var action = () => _engine.AdjustPacing(new AdjustBidPacingRequest("non_existent_campaign", 1.0, PacingMode.EvenThroughoutDay));

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Theory]
    [InlineData(0.5, PacingMode.ThrottledOverSpend)]
    [InlineData(2.5, PacingMode.AcceleratedHighIntent)]
    [InlineData(1.0, PacingMode.EvenThroughoutDay)]
    public void AdjustPacing_Should_Support_Various_Multipliers_And_Modes(double multiplier, PacingMode mode)
    {
        var campaignId = "camp_meta_lookalike_03";
        var request = new AdjustBidPacingRequest(campaignId, multiplier, mode);

        var result = _engine.AdjustPacing(request);

        result.UpdatedMultiplier.Should().Be(multiplier);
        result.Mode.Should().Be(mode);
        result.CampaignId.Should().Be(campaignId);
        result.StatusSummary.Should().Contain("FinTech SaaS Buying Committee");
    }
}
