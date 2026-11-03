using FluentAssertions;
using SGrowing.Api.Modules.Finance.PayrollTaxWithholding;
using Xunit;

namespace SGrowing.Tests.Finance;

public class PayrollTaxWithholdingTests
{
    private readonly PayrollTaxWithholdingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsMonitoredJurisdictionsAndFilings()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.MonitoredJurisdictionsCount.Should().Be(4);
        overview.TotalMonthlyWithheldUsd.Should().BeGreaterThan(1000000m);
        overview.PendingStatutoryFilingsCount.Should().BeGreaterThanOrEqualTo(1);
        overview.Jurisdictions.Should().HaveCount(4);
        overview.RecentFilings.Should().HaveCount(2);
    }

    [Fact]
    public void CalculateWithholding_ComputesAccurateStatutoryDeductions()
    {
        var request = new CalculateWithholdingRequest(
            "EMP-ALEX-771",
            "US-FED-CA",
            10000m,
            "MONTHLY",
            2
        );

        var result = _engine.CalculateWithholding(request);

        result.Should().NotBeNull();
        result.EmployeeId.Should().Be("EMP-ALEX-771");
        result.JurisdictionCode.Should().Be("US-FED-CA");
        result.GrossPay.Should().Be(10000m);
        result.IncomeTaxWithheld.Should().Be(2400m); // 24%
        result.SocialSecurityWithheld.Should().Be(765m); // 7.65%
        result.PensionWithheld.Should().Be(400m); // 4%
        result.NetPay.Should().Be(6435m); // 10000 - 2400 - 765 - 400
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void GenerateFilingReceipt_EmitsCryptographicMerkleDigest()
    {
        var request = new GenerateFilingReceiptRequest(
            "GB-PAYE",
            "2026-11",
            215000m,
            "BARCLAYS-REMIT-998811"
        );

        var receipt = _engine.GenerateFilingReceipt(request);

        receipt.Should().NotBeNull();
        receipt.ReceiptId.Should().StartWith("RCP-TAX-");
        receipt.JurisdictionCode.Should().Be("GB-PAYE");
        receipt.TotalTaxRemitted.Should().Be(215000m);
        receipt.MerkleAuditDigest.Should().StartWith("0x");
        receipt.Status.Should().Be("REMITTANCE_FINALIZED");
    }
}
