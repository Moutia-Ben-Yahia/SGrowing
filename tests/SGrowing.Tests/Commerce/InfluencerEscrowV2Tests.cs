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
}
