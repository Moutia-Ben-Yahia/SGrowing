using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Advertising.RetailMediaAuction;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class RetailMediaAuctionTests
{
    private readonly RetailMediaAuctionEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsRmnMetricsAndRecentAuctions()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalAuctionsExecuted24h.Should().BeGreaterThan(10000);
        overview.TotalAdRevenueGenerated.Should().BeGreaterThan(0);
        overview.AverageClearingCpc.Should().BeGreaterThan(1.0m);
        overview.AverageMerchantRoas.Should().BeGreaterThan(4.0);
        overview.RecentAuctions.Should().NotBeEmpty();
    }

    [Fact]
    public void RunAuction_ExecutesGspSecondPriceAuctionAccurately()
    {
        var bids = new List<MerchantAdBid>
        {
            new("BID-1", "Merchant A", "SKU-A", "wireless speaker", 4.00m, 0.05, 0.8), // Rank = 4.00 * 0.05 * 0.8 = 0.160
            new("BID-2", "Merchant B", "SKU-B", "wireless speaker", 3.00m, 0.04, 0.9), // Rank = 3.00 * 0.04 * 0.9 = 0.108
            new("BID-3", "Merchant C", "SKU-C", "wireless speaker", 2.00m, 0.03, 0.7)  // Rank = 2.00 * 0.03 * 0.7 = 0.042
        };

        var command = new RunAuctionCommand("SearchTopBanner", "wireless speaker", bids);
        var result = _engine.RunAuction(command);

        result.Should().NotBeNull();
        result.WinningMerchantName.Should().Be("Merchant A");
        result.WinningSku.Should().Be("SKU-A");
        result.WinningBidAmount.Should().Be(4.00m);
        // Clearing price is second rank (0.108) / (0.05 * 0.8 = 0.04) + 0.01 = 2.70 + 0.01 = 2.71
        result.ClearingPrice.Should().Be(2.71m);
        result.ComputedRankScore.Should().Be(0.160);
    }

    [Fact]
    public void RunAuction_ThrowsWhenBidsEmpty()
    {
        var command = new RunAuctionCommand("SearchTopBanner", "test", new List<MerchantAdBid>());
        var act = () => _engine.RunAuction(command);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one*");
    }
}
