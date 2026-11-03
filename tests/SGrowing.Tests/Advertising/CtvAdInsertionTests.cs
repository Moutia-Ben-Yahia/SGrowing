using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Advertising.CtvAdInsertion;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class CtvAdInsertionTests
{
    private readonly CtvAdInsertionEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActivePodsAndAttributions()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActivePodsCount.Should().Be(4);
        overview.AverageClearingCpm.Should().BeGreaterThan(15m);
        overview.TotalImpressionsToday.Should().BeGreaterThan(100000);
        overview.QrScanConversionRatePct.Should().BeGreaterThan(1.0);
        overview.Pods.Should().HaveCount(4);
        overview.RecentAttributions.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessBidStream_ClearsAboveFloorBid()
    {
        var command = new ProcessBidStreamCommand("POD-ROKU-881", "ADV-NIKE-01", "Spring Air Campaign", 28.00m, true);
        var response = _engine.ProcessBidStream(command);

        response.Should().NotBeNull();
        response.ClearingStatus.Should().Be("Cleared");
        response.VastTagUrl.Should().Contain("vast/v42/creative");
        response.QrWatermarkEnabled.Should().BeTrue();
    }

    [Fact]
    public void ProcessBidStream_RejectsBelowFloorBid()
    {
        var command = new ProcessBidStreamCommand("POD-ROKU-881", "ADV-LOW-01", "Discount Mattress", 12.00m, false);
        var response = _engine.ProcessBidStream(command);

        response.Should().NotBeNull();
        response.ClearingStatus.Should().Be("Outbid");
    }

    [Fact]
    public void AttributeScan_CorrelatesMobileConversionWithBroadcastSession()
    {
        var command = new AttributeScanCommand("QR-SESS-9921", "iOS", 320.00m, "CUST-APP-99");
        var result = _engine.AttributeScan(command);

        result.Should().NotBeNull();
        result.QrCodeSessionId.Should().Be("QR-SESS-9921");
        result.MatchedPodId.Should().Be("POD-ROKU-881");
        result.AttributedRevenue.Should().Be(320.00m);
        result.AttributionMatchReceipt.Should().Contain("RECEIPT-CTV-QR");
    }
}
