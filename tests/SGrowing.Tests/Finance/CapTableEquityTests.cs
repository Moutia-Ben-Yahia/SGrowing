using System;
using System.Linq;
using SGrowing.Api.Modules.Finance.CapTableEquity;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class CapTableEquityTests
{
    [Fact]
    public void GetOverview_ReturnsInitialStakeholdersAndOptionGrants()
    {
        var engine = new CapTableEquityEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.FullyDilutedSharesTotal > 0);
        Assert.True(overview.Latest409AValuationUsd > 0);
        Assert.True(overview.ActiveStakeholdersCount >= 3);
        Assert.NotEmpty(overview.Stakeholders);
        Assert.NotEmpty(overview.OptionGrants);
    }

    [Fact]
    public void GrantStockOption_AppendsValidVestingRecord()
    {
        var engine = new CapTableEquityEngine();
        var request = new GrantStockOptionRequest(
            "Alexandre Dubois (VP Sales)",
            50000,
            2.50m,
            12,
            48
        );

        var grant = engine.GrantStockOption(request);

        Assert.NotNull(grant);
        Assert.StartsWith("GRT-", grant.GrantId);
        Assert.Equal("Alexandre Dubois (VP Sales)", grant.EmployeeName);
        Assert.Equal(50000, grant.GrantedSharesCount);
        Assert.Equal("VESTING_ACTIVE", grant.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.OptionGrants, g => g.GrantId == grant.GrantId);
    }

    [Fact]
    public void SimulateDilution_CalculatesPostMoneySyndicateOwnership()
    {
        var engine = new CapTableEquityEngine();
        var request = new SimulateDilutionRequest(
            20000000.00m, // $20M new investment
            80000000.00m, // $80M pre-money => $100M post-money (20% dilution)
            "SERIES_B"
        );

        var stakeholder = engine.SimulateDilution(request);

        Assert.NotNull(stakeholder);
        Assert.StartsWith("STK-SERIES_B-", stakeholder.StakeholderId);
        Assert.Equal(20.0, stakeholder.OwnershipPercentagePct);
        Assert.Equal(20000000.00m, stakeholder.InvestedCapitalUsd);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Stakeholders, s => s.StakeholderId == stakeholder.StakeholderId);
    }
}
