using FluentAssertions;
using SGrowing.Api.Modules.Finance.SovereignEInvoicing;
using Xunit;

namespace SGrowing.Tests.Finance;

public class SovereignEInvoicingTests
{
    private readonly SovereignEInvoicingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsPortalsAndClearedInvoices()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalInvoicesClearedCount.Should().Be(2);
        overview.ClearanceSuccessRatePct.Should().Be(100.0);
        overview.ActiveSovereignPortalsCount.Should().Be(4);
        overview.SupportedPortals.Should().HaveCount(4);
        overview.RecentClearedInvoices.Should().HaveCount(2);
    }

    [Fact]
    public void GenerateClearanceInvoice_ProducesSignedUblWithTaxSeal()
    {
        var request = new GenerateClearanceInvoiceRequest(
            "INV-TEST-2026-9999",
            "PEPPOL_EU",
            "DE987654321",
            "FR1122334455",
            50000.00m,
            20.0m, // 20% VAT
            "EUR"
        );

        var result = _engine.GenerateClearanceInvoice(request);

        result.Should().NotBeNull();
        result.InvoiceNumber.Should().Be("INV-TEST-2026-9999");
        result.SignedXmlUblPayload.Should().Contain("<Invoice");
        result.SovereignClearanceUuid.Should().StartWith("UUID-PEPPOL_EU-");
        result.CryptographicTaxSeal.Should().StartWith("0x");
        result.ClearanceStatus.Should().Be("CLEARED_BY_TAX_AUTHORITY");
        result.LatencyMs.Should().BeLessThan(200);
    }

    [Fact]
    public void VerifyClearance_ReturnsValidatedAuthorityDigest()
    {
        var request = new VerifyClearanceRequest(
            "INV-EU-2026-8899",
            "UUID-PEPPOL-0x99AABB1122"
        );

        var receipt = _engine.VerifyClearance(request);

        receipt.Should().NotBeNull();
        receipt.InvoiceNumber.Should().Be("INV-EU-2026-8899");
        receipt.IsValid.Should().BeTrue();
        receipt.SovereignAuthorityConfirmation.Should().Contain("VALIDATED");
    }
}
