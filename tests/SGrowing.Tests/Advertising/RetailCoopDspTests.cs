using System;
using System.Linq;
using SGrowing.Api.Modules.Advertising.RetailCoopDsp;
using Xunit;

namespace SGrowing.Tests.Advertising;

public sealed class RetailCoopDspTests
{
    [Fact]
    public void GetOverview_ReturnsInitialCleanRoomsAndCampaigns()
    {
        var engine = new RetailCoopDspEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ConnectedRetailCleanRoomsCount >= 2);
        Assert.True(overview.ActiveCoopCampaignsCount >= 2);
        Assert.True(overview.TotalCoopSpendUnderManagementUsd > 0);
        Assert.True(overview.BlendedClosedLoopRoas > 1.0);
        Assert.NotEmpty(overview.CleanRoomPartners);
        Assert.NotEmpty(overview.ActiveCampaigns);
    }

    [Fact]
    public void LaunchCoopCampaign_AllocatesRetailerContributionCorrectly()
    {
        var engine = new RetailCoopDspEngine();
        var request = new LaunchCoopCampaignRequest(
            "VoltPure Nutrition",
            "CR-WALMART-01",
            50000.00m,
            50.0m, // 50% retailer co-op match = 25,000 credit
            "Frequent Grocery Protein Powder Shoppers",
            "CTV & Open Web Native"
        );

        var campaign = engine.LaunchCoopCampaign(request);

        Assert.NotNull(campaign);
        Assert.StartsWith("COOP-", campaign.CampaignId);
        Assert.Equal(50000.00m, campaign.BrandContributionUsd);
        Assert.Equal(25000.00m, campaign.RetailerCoopCreditUsd);
        Assert.Equal(75000.00m, campaign.TotalCoopBudgetUsd);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveCampaigns, c => c.CampaignId == campaign.CampaignId);
    }

    [Fact]
    public void ExecuteCleanRoomMatch_ReturnsCryptographicProofAndMatchRate()
    {
        var engine = new RetailCoopDspEngine();
        var request = new ExecuteCleanRoomMatchRequest("CR-AMAZON-02", 100000);

        var receipt = engine.ExecuteCleanRoomMatch(request);

        Assert.NotNull(receipt);
        Assert.StartsWith("RCP-CR-", receipt.MatchReceiptId);
        Assert.StartsWith("0x", receipt.CryptographicMatchProof);
        Assert.True(receipt.MatchedHighPropensityShoppers > 50000);
        Assert.True(receipt.MatchRatePct > 50.0);
    }
}
