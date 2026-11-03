using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.VendorBillMatching;
using Xunit;

namespace SGrowing.Tests.Finance;

public class VendorBillMatchingTests
{
    private readonly VendorBillMatchingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsMatchedBillsAndSavings()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.PendingBillsCount.Should().BeGreaterThanOrEqualTo(2);
        overview.TotalBillVolume.Should().BeGreaterThan(300000m);
        overview.TotalEarlyDiscountsCaptured.Should().BeGreaterThan(0);
        overview.PerfectMatchesCount.Should().BeGreaterThanOrEqualTo(2);
        overview.Bills.Should().NotBeEmpty();
    }

    [Fact]
    public void ApproveEarlyDiscount_SettlesDiscountAndReturnsNet()
    {
        var command = new ApproveEarlyDiscountCommand("BILL-2026-4401", "CFO-TREASURY-01");
        var result = _engine.ApproveEarlyDiscount(command);

        result.Should().NotBeNull();
        result.BillId.Should().Be("BILL-2026-4401");
        result.NetPaymentAmount.Should().Be(142100m); // 145,000 - 2,900
        result.SavedDiscountAmount.Should().Be(2900m);
        result.SettlementReceipt.Should().Contain("DISCOUNT-ESCROW-PAID");
    }

    [Fact]
    public void ApproveEarlyDiscount_RejectsDiscrepantBill()
    {
        var command = new ApproveEarlyDiscountCommand("BILL-2026-4403", "CFO-TREASURY-01");
        var act = () => _engine.ApproveEarlyDiscount(command);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*discrepancy*");
    }
}
