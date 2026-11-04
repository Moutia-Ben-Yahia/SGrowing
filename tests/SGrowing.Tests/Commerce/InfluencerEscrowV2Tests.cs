using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Commerce.InfluencerEscrowV2;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class InfluencerEscrowV2Tests
{
    private readonly InfluencerEscrowV2Engine _engine = new();

    [Fact]
    public void GetCampaigns_ReturnsRankedByGmv()
    {
        var list = _engine.GetCampaigns();

        list.Should().NotBeNull();
        list.Should().HaveCount(3);
        list.First().CreatorHandle.Should().Be("@SarahGrowthLead");
        list.First().AttributedGmvUsd.Should().Be(310000m);
    }

    [Fact]
    public void ReleasePayout_DisbursesEscrowCorrectly()
    {
        var campaignId = "inf_camp_201";
        // Total 15000, 5000 released, 10000 available
        var request = new ReleaseMilestonePayoutRequest(campaignId, 4000m);

        var updated = _engine.ReleasePayout(request);

        updated.ReleasedPayoutUsd.Should().Be(9000m);
    }

    [Fact]
    public void AttributeConversion_IncrementsGmv_WhenLegitimate()
    {
        var campaignId = "inf_camp_203";
        var request = new AttributeConversionRequest(campaignId, OrderGmvUsd: 1500m, IsFraudSuspected: false);

        var updated = _engine.AttributeConversion(request);

        updated.AttributedConversions.Should().Be(19);
        updated.AttributedGmvUsd.Should().Be(37500m);
    }

    [Fact]
    public void AttributeConversion_FlagsFraud_WhenSuspected()
    {
        var campaignId = "inf_camp_203";
        var beforeScore = _engine.GetCampaign(campaignId)!.FraudRiskScore;
        var request = new AttributeConversionRequest(campaignId, OrderGmvUsd: 1500m, IsFraudSuspected: true);

        var updated = _engine.AttributeConversion(request);

        updated.FraudRiskScore.Should().BeGreaterThan(beforeScore);
        updated.AttributedConversions.Should().Be(18); // Not incremented
    }

    [Fact]
    public void GetCampaign_ReturnsNull_WhenCampaignDoesNotExist()
    {
        var campaign = _engine.GetCampaign("nonexistent_camp_000");

        campaign.Should().BeNull();
    }

    [Fact]
    public void ReleasePayout_ThrowsKeyNotFoundException_WhenCampaignNotFound()
    {
        var request = new ReleaseMilestonePayoutRequest("ghost_campaign_id", 1000m);

        var act = () => _engine.ReleasePayout(request);

        act.Should().Throw<KeyNotFoundException>().WithMessage("*ghost_campaign_id*");
    }

    [Fact]
    public void ReleasePayout_ClampsAmount_WhenRequestedExceedsMaxAvailable()
    {
        var campaignId = "inf_camp_201"; // Total: 15,000, Released: 5,000, Available: 10,000
        var request = new ReleaseMilestonePayoutRequest(campaignId, 999999m); // Huge amount

        var updated = _engine.ReleasePayout(request);

        // Clamped to 5000 + 10000 = 15000
        updated.ReleasedPayoutUsd.Should().Be(15000m);
    }

    [Fact]
    public void AttributeConversion_CapsFraudRiskScore_At100Percent()
    {
        var campaignId = "inf_camp_202";

        // Repeated fraud hits
        for (int i = 0; i < 15; i++)
        {
            _engine.AttributeConversion(new AttributeConversionRequest(campaignId, 100m, IsFraudSuspected: true));
        }

        var campaign = _engine.GetCampaign(campaignId);
        campaign.Should().NotBeNull();
        campaign!.FraudRiskScore.Should().Be(100.0);
    }
}
