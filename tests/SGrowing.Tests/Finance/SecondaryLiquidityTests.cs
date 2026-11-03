using SGrowing.Api.Modules.Finance.SecondaryLiquidity;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class SecondaryLiquidityTests
{
    private readonly SecondaryLiquidityEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededTenderOffersAndLots()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveTenderOffersCount >= 1);
        Assert.True(overview.TotalSecondaryVolumeSettledUsd > 10000000);
        Assert.NotEmpty(overview.ActiveOffers);
        Assert.NotEmpty(overview.RecentLots);
    }

    [Fact]
    public void SubmitShareLot_CalculatesAccurateWithholdingAndVolume()
    {
        var lot = _engine.SubmitShareLot("TENDER-2026-Q4", "SH-EMP-550", "Elena Rostova", 5000);

        Assert.NotNull(lot);
        Assert.StartsWith("LOT-SEC-", lot.LotId);
        Assert.Equal(5000 * 42.50, lot.AggregateValueUsd);
        Assert.Equal(lot.AggregateValueUsd * 0.37, lot.EstimatedTaxWithholdingUsd);
        Assert.Equal("PENDING_ROFR", lot.Status);
    }

    [Fact]
    public void ProcessRofrClearance_CompanyExercise_RepurchasesIntoTreasury()
    {
        var lot = _engine.SubmitShareLot("TENDER-2026-Q4", "SH-EMP-771", "David Miller", 1000);
        var token = _engine.ProcessRofrClearance(lot.LotId, companyExercises: true);

        Assert.NotNull(token);
        Assert.StartsWith("ROFR-TOK-", token.TokenId);
        Assert.True(token.CompanyExercisedRofr);
        Assert.Contains("Share Buyback", token.ApprovedPurchaserEntity);
        Assert.Equal(64, token.CryptographicSignatureSha256.Length);
    }
}
