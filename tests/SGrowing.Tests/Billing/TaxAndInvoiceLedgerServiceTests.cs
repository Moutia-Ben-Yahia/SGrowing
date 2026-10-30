using System.Text;
using FluentAssertions;
using SGrowing.Api.Modules.Billing;
using Xunit;

namespace SGrowing.Tests.Billing;

public class TaxAndInvoiceLedgerServiceTests
{
    private readonly TaxAndInvoiceLedgerService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public TaxAndInvoiceLedgerServiceTests()
    {
        _service = new TaxAndInvoiceLedgerService();
    }

    [Fact]
    public void FxConversion_WithSafetySpreadBuffer_CalculatesEffectiveRateCorrectly()
    {
        // Spot rate: 0.92 EUR/USD, Spread: 1.5%
        // Effective rate = 0.92 * (1 + 0.015) = 0.9338
        decimal effectiveRate = _service.CalculateEffectiveRate(0.92m, 1.5m);
        effectiveRate.Should().Be(0.9338m);

        // USD base has 0% spread -> 1.0
        decimal usdEffective = _service.CalculateEffectiveRate(1.0m, 0.0m);
        usdEffective.Should().Be(1.0m);

        // SAR: 3.75 with 1.2% spread -> 3.75 * 1.012 = 3.795
        decimal sarEffective = _service.CalculateEffectiveRate(3.75m, 1.2m);
        sarEffective.Should().Be(3.795m);
    }

    [Fact]
    public void FxConversion_ConvertsAmountBetweenCurrencies_Accurately()
    {
        // 100 USD to EUR at effective rate 0.9338 => 93.38 EUR
        decimal eurAmount = _service.ConvertCurrency(100m, "USD", "EUR");
        eurAmount.Should().Be(93.38m);

        // Same currency returns original amount
        decimal identity = _service.ConvertCurrency(250.50m, "USD", "USD");
        identity.Should().Be(250.50m);
    }

    [Fact]
    public void GetCurrencies_ReturnsPreSeededSpotRates_WithSupportedCurrencies()
    {
        var currencies = _service.GetCurrencies();
        currencies.Should().NotBeEmpty();
        currencies.Select(c => c.Code).Should().Contain(new[] { "USD", "EUR", "GBP", "SAR", "AED", "TND" });
    }

    [Fact]
    public void EuViesValidation_France_ValidatesCorrectFormat()
    {
        // Valid French VAT: FR + 2 alphanumeric + 9 digits
        _service.ValidateViesFormat("FR", "FR32123456789").Should().BeTrue();
        _service.ValidateViesFormat("FR", "FRXX123456789").Should().BeTrue();
        // Whitespace and dash tolerance
        _service.ValidateViesFormat("FR", "FR 32 123-456-789").Should().BeTrue();
        // Missing prefix auto-prepended
        _service.ValidateViesFormat("FR", "32123456789").Should().BeTrue();

        // Invalid format
        _service.ValidateViesFormat("FR", "FR123").Should().BeFalse();
        _service.ValidateViesFormat("FR", "INVALID_VAT").Should().BeFalse();
    }

    [Fact]
    public void EuViesValidation_Germany_ValidatesCorrectFormat()
    {
        // Valid German VAT: DE + 9 digits
        _service.ValidateViesFormat("DE", "DE123456789").Should().BeTrue();
        _service.ValidateViesFormat("DE", "DE 123 456 789").Should().BeTrue();
        _service.ValidateViesFormat("DE", "123456789").Should().BeTrue();

        // Invalid
        _service.ValidateViesFormat("DE", "DE12345678").Should().BeFalse(); // 8 digits
        _service.ValidateViesFormat("DE", "DE1234567890").Should().BeFalse(); // 10 digits
    }

    [Fact]
    public void EuViesValidation_Italy_ValidatesCorrectFormat()
    {
        // Valid Italian VAT: IT + 11 digits
        _service.ValidateViesFormat("IT", "IT12345678901").Should().BeTrue();
        _service.ValidateViesFormat("IT", "12345678901").Should().BeTrue();

        // Invalid
        _service.ValidateViesFormat("IT", "IT1234567890").Should().BeFalse();
    }

    [Fact]
    public void EuViesValidation_Spain_ValidatesCorrectFormat()
    {
        // Valid Spanish VAT: ES + 1 letter/digit + 7 digits + 1 letter/digit
        _service.ValidateViesFormat("ES", "ESA1234567B").Should().BeTrue();
        _service.ValidateViesFormat("ES", "ESB12345678").Should().BeTrue();

        // Invalid
        _service.ValidateViesFormat("ES", "ES123").Should().BeFalse();
    }

    [Fact]
    public async Task CrossBorderEuB2B_WithValidVies_TriggersZeroPercentReverseCharge()
    {
        var request = new TaxEvaluationRequest(
            MerchantCountry: "FR",
            CustomerCountry: "DE",
            IsB2B: true,
            CustomerVatNumber: "DE123456789",
            Subtotal: 10000m
        );

        var result = await _service.EvaluateTaxAsync(request);

        result.ApplicableTaxRatePct.Should().Be(0.0m);
        result.TaxAmount.Should().Be(0.0m);
        result.IsReverseCharge.Should().BeTrue();
        result.TaxScheme.Should().Be("EU Cross-Border B2B Reverse Charge");
        result.ExemptionReason.Should().Contain("Reverse charge: Art. 196 EU VAT Directive");
        result.ViesValid.Should().BeTrue();
    }

    [Fact]
    public async Task DomesticEuSales_ChargeStandardDomesticVatRate()
    {
        // Merchant in FR, Customer in FR -> 20% TVA
        var request = new TaxEvaluationRequest(
            MerchantCountry: "FR",
            CustomerCountry: "FR",
            IsB2B: true,
            CustomerVatNumber: "FR32123456789",
            Subtotal: 5000m
        );

        var result = await _service.EvaluateTaxAsync(request);

        result.ApplicableTaxRatePct.Should().Be(20.0m);
        result.TaxAmount.Should().Be(1000.0m);
        result.IsReverseCharge.Should().BeFalse();
        result.TaxScheme.Should().Be("Domestic EU TVA");
        result.ExemptionReason.Should().BeNull();
    }

    [Fact]
    public async Task CrossBorderEuB2C_OrInvalidVies_AppliesDestinationVat()
    {
        // Customer in DE (19% MwSt), but B2C or invalid VIES
        var request = new TaxEvaluationRequest(
            MerchantCountry: "FR",
            CustomerCountry: "DE",
            IsB2B: false,
            CustomerVatNumber: null,
            Subtotal: 2000m
        );

        var result = await _service.EvaluateTaxAsync(request);

        result.ApplicableTaxRatePct.Should().Be(19.0m);
        result.TaxAmount.Should().Be(380.0m);
        result.IsReverseCharge.Should().BeFalse();
        result.TaxScheme.Should().Contain("EU B2C Destination");
        result.ViesValid.Should().BeFalse();
    }

    [Fact]
    public async Task SaudiArabiaSales_ChargeFifteenPercentZatcaVat()
    {
        var request = new TaxEvaluationRequest(
            MerchantCountry: "SA",
            CustomerCountry: "SA",
            IsB2B: true,
            CustomerVatNumber: "310123456700003",
            Subtotal: 20000m
        );

        var result = await _service.EvaluateTaxAsync(request);

        result.ApplicableTaxRatePct.Should().Be(15.0m);
        result.TaxAmount.Should().Be(3000.0m);
        result.TaxScheme.Should().Be("KSA ZATCA VAT");
        result.IsReverseCharge.Should().BeFalse();
    }

    [Fact]
    public void ZatcaTlvEncoding_GeneratesCompliantBase64String_WithAllFiveTags()
    {
        var timestamp = new DateTime(2026, 10, 30, 14, 0, 0, DateTimeKind.Utc);
        string qrBase64 = _service.GenerateZatcaQr(
            sellerName: "SGrowing Saudi Arabia Ltd",
            sellerVatNumber: "310123456700003",
            timestampUtc: timestamp,
            totalAmount: 11500.00m,
            vatAmount: 1500.00m
        );

        qrBase64.Should().NotBeNullOrWhiteSpace();

        // Verify that Base64 decodes back to the 5 TLV tags
        var decodedTags = TaxAndInvoiceLedgerService.DecodeZatcaQr(qrBase64);
        decodedTags.Should().ContainKey(1).WhoseValue.Should().Be("SGrowing Saudi Arabia Ltd");
        decodedTags.Should().ContainKey(2).WhoseValue.Should().Be("310123456700003");
        decodedTags.Should().ContainKey(3).WhoseValue.Should().Be("2026-10-30T14:00:00Z");
        decodedTags.Should().ContainKey(4).WhoseValue.Should().Be("11500.00");
        decodedTags.Should().ContainKey(5).WhoseValue.Should().Be("1500.00");
    }

    [Fact]
    public async Task UaeSales_ChargeFivePercentFtaVat()
    {
        var request = new TaxEvaluationRequest(
            MerchantCountry: "AE",
            CustomerCountry: "AE",
            IsB2B: true,
            CustomerVatNumber: "100234567800003",
            Subtotal: 10000m
        );

        var result = await _service.EvaluateTaxAsync(request);

        result.ApplicableTaxRatePct.Should().Be(5.0m);
        result.TaxAmount.Should().Be(500.0m);
        result.TaxScheme.Should().Be("UAE FTA VAT");
    }

    [Fact]
    public async Task TunisiaDomesticSales_ChargeNineteenPercentTva()
    {
        var request = new TaxEvaluationRequest(
            MerchantCountry: "TN",
            CustomerCountry: "TN",
            IsB2B: true,
            CustomerVatNumber: "1234567A/P/M/000",
            Subtotal: 10000m
        );

        var result = await _service.EvaluateTaxAsync(request);

        result.ApplicableTaxRatePct.Should().Be(19.0m);
        result.TaxAmount.Should().Be(1900.0m);
        result.TaxScheme.Should().Be("Tunisian Domestic VAT");
    }

    [Fact]
    public async Task NonResidentExport_IsZeroRatedExport()
    {
        var request = new TaxEvaluationRequest(
            MerchantCountry: "FR",
            CustomerCountry: "US",
            IsB2B: true,
            CustomerVatNumber: null,
            Subtotal: 15000m
        );

        var result = await _service.EvaluateTaxAsync(request);

        result.ApplicableTaxRatePct.Should().Be(0.0m);
        result.TaxAmount.Should().Be(0.0m);
        result.TaxScheme.Should().Be("Zero-Rated International Export");
        result.ExemptionReason.Should().NotBeNull();
    }

    [Fact]
    public async Task IssueInvoice_AssignsSequentialInvoiceNumber_AndCalculatesTotals()
    {
        var request = new CreateCommercialInvoiceRequest(
            CustomerName: "Nordic Ventures AB",
            CustomerEmail: "nordic@ventures.se",
            CustomerCountry: "DE",
            CustomerVatNumber: "DE123456789",
            IsB2B: true,
            Currency: "EUR",
            LineItems: new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Dedicated Voice AI Server", Quantity = 2, UnitPrice = 3000m, TotalPrice = 6000m }
            },
            MerchantCountry: "FR"
        );

        var invoice = await _service.IssueInvoiceAsync(_tenantId, request);

        invoice.Should().NotBeNull();
        invoice.InvoiceNumber.Should().MatchRegex(@"^INV-2026-\d{5}$");
        invoice.Subtotal.Should().Be(6000m);
        invoice.TaxRatePct.Should().Be(0.0m); // Reverse charge
        invoice.TotalAmount.Should().Be(6000m);
        invoice.RemainingBalance.Should().Be(6000m);
        invoice.Status.Should().Be(CommercialInvoiceStatus.Issued);
        invoice.FacturXXmlPayload.Should().Contain("<rsm:CrossIndustryInvoice");
    }

    [Fact]
    public async Task IssueInvoice_ForSaudiCustomer_GeneratesZatcaQrBase64()
    {
        var request = new CreateCommercialInvoiceRequest(
            CustomerName: "Jeddah Trade Corp",
            CustomerEmail: "finance@jeddahtrade.sa",
            CustomerCountry: "SA",
            CustomerVatNumber: "310123456700003",
            IsB2B: true,
            Currency: "SAR",
            LineItems: new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Logistics Telematics Suite", Quantity = 1, UnitPrice = 20000m, TotalPrice = 20000m }
            },
            MerchantCountry: "SA"
        );

        var invoice = await _service.IssueInvoiceAsync(_tenantId, request);

        invoice.TaxRatePct.Should().Be(15.0m);
        invoice.TaxAmount.Should().Be(3000.0m);
        invoice.TotalAmount.Should().Be(23000.0m);
        invoice.ZatcaQrBase64.Should().NotBeNullOrWhiteSpace();

        var decoded = TaxAndInvoiceLedgerService.DecodeZatcaQr(invoice.ZatcaQrBase64!);
        decoded[1].Should().Be("SGrowing Saudi Arabia Ltd");
        decoded[4].Should().Be("23000.00");
        decoded[5].Should().Be("3000.00");
    }

    [Fact]
    public async Task IssueCreditNote_ReducesInvoiceBalance_AndAssignsSequentialNumber()
    {
        // 1. Issue an invoice with domestic FR VAT (20%)
        var invReq = new CreateCommercialInvoiceRequest(
            CustomerName: "Marseille Shipping SARL",
            CustomerEmail: "ops@marseille-shipping.fr",
            CustomerCountry: "FR",
            CustomerVatNumber: "FR32123456789",
            IsB2B: true,
            Currency: "EUR",
            LineItems: new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Logistics Voice Agent", Quantity = 1, UnitPrice = 1000m, TotalPrice = 1000m }
            },
            MerchantCountry: "FR"
        );

        var invoice = await _service.IssueInvoiceAsync(_tenantId, invReq);
        invoice.TotalAmount.Should().Be(1200m); // 1000 + 200 VAT
        invoice.RemainingBalance.Should().Be(1200m);

        // 2. Issue partial credit note of 600 EUR (gross)
        var cnReq = new IssueCreditNoteRequest(
            Reason: "Partial SLA outage rebate",
            RefundAmount: 600m,
            IsGrossAmount: true
        );

        var creditNote = await _service.IssueCreditNoteAsync(_tenantId, invoice.Id, cnReq);

        creditNote.Should().NotBeNull();
        creditNote.CreditNoteNumber.Should().MatchRegex(@"^CN-2026-\d{5}$");
        creditNote.TotalCredit.Should().Be(600m);
        creditNote.TaxAdjustment.Should().Be(100m); // 600 * (20 / 120) = 100
        creditNote.RefundAmount.Should().Be(500m);

        // Invoice should reflect reduced balance and PartiallyRefunded status
        invoice.RemainingBalance.Should().Be(600m);
        invoice.CreditNotesApplied.Should().Be(600m);
        invoice.Status.Should().Be(CommercialInvoiceStatus.PartiallyRefunded);
    }

    [Fact]
    public async Task IssueCreditNote_ForHundredPercentAmount_UpdatesInvoiceStatusToReversed()
    {
        var invReq = new CreateCommercialInvoiceRequest(
            CustomerName: "Lyon Digital Agency",
            CustomerEmail: "contact@lyon-digital.fr",
            CustomerCountry: "FR",
            CustomerVatNumber: "FR32123456789",
            IsB2B: true,
            Currency: "EUR",
            LineItems: new List<CommercialInvoiceLineItem>
            {
                new() { Description = "API Subscription Tier", Quantity = 1, UnitPrice = 500m, TotalPrice = 500m }
            },
            MerchantCountry: "FR"
        );

        var invoice = await _service.IssueInvoiceAsync(_tenantId, invReq);
        invoice.TotalAmount.Should().Be(600m);

        // Issue 100% full reversal
        var cnReq = new IssueCreditNoteRequest(
            Reason: "Full contract cancellation within 14-day cooling off period",
            RefundAmount: 600m,
            IsGrossAmount: true
        );

        var creditNote = await _service.IssueCreditNoteAsync(_tenantId, invoice.Id, cnReq);

        creditNote.TotalCredit.Should().Be(600m);
        invoice.RemainingBalance.Should().Be(0m);
        invoice.Status.Should().Be(CommercialInvoiceStatus.Reversed);
    }

    [Fact]
    public async Task IssueCreditNote_PreventedFromExceedingInvoiceTotal_ThrowsInvalidOperationException()
    {
        var invReq = new CreateCommercialInvoiceRequest(
            CustomerName: "Bordeaux Analytics",
            CustomerEmail: "info@bordeaux-analytics.fr",
            CustomerCountry: "FR",
            CustomerVatNumber: "FR32123456789",
            IsB2B: true,
            Currency: "EUR",
            LineItems: new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Consulting Block", Quantity = 1, UnitPrice = 1000m, TotalPrice = 1000m }
            },
            MerchantCountry: "FR"
        );

        var invoice = await _service.IssueInvoiceAsync(_tenantId, invReq); // Total = 1200 EUR

        // Attempting to credit 1500 EUR against 1200 EUR invoice
        var excessReq = new IssueCreditNoteRequest(
            Reason: "Excessive refund attempt",
            RefundAmount: 1500m,
            IsGrossAmount: true
        );

        var act = () => _service.IssueCreditNoteAsync(_tenantId, invoice.Id, excessReq);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds remaining balance*");
    }

    [Fact]
    public async Task TaxLiabilitySummary_AggregatesCollectedTaxAndAdjustments_ByJurisdiction()
    {
        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var summary = await _service.GetTaxLiabilitySummaryAsync(defaultTenantId);

        summary.Should().NotBeEmpty();

        // Check Saudi Arabia pre-seeded invoice (15% VAT)
        var saSummary = summary.FirstOrDefault(s => s.Jurisdiction == "SA");
        saSummary.Should().NotBeNull();
        saSummary!.Currency.Should().Be("SAR");
        saSummary.TotalTaxCollected.Should().Be(6750.00m);

        // Check Tunisia pre-seeded invoice with credit note adjustment
        var tnSummary = summary.FirstOrDefault(s => s.Jurisdiction == "TN");
        tnSummary.Should().NotBeNull();
        tnSummary!.Currency.Should().Be("TND");
        tnSummary.TotalTaxCollected.Should().Be(3420.00m);
        tnSummary.TotalCreditsAdjusted.Should().Be(570.00m);
        tnSummary.NetTaxLiability.Should().Be(2850.00m);
    }

    [Fact]
    public void FacturXXmlGeneration_ProducesCompliantElectronicInvoice_WithValidElements()
    {
        var invoice = new CommercialInvoice
        {
            InvoiceNumber = "INV-2026-99999",
            IssueDate = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc),
            Currency = "EUR",
            Subtotal = 10000.00m,
            TaxAmount = 2000.00m,
            TotalAmount = 12000.00m,
            RemainingBalance = 12000.00m,
            CustomerName = "Airbus Operations SAS",
            CustomerCountry = "FR",
            CustomerVatNumber = "FR12345678901"
        };

        string xml = _service.GenerateFacturXXml(
            invoice,
            sellerName: "SGrowing Enterprise Technologies SAS",
            sellerVatNumber: "FR88990011223",
            sellerCountry: "FR"
        );

        xml.Should().NotBeNullOrWhiteSpace();
        xml.Should().Contain("<rsm:CrossIndustryInvoice");
        xml.Should().Contain("<ram:ID>INV-2026-99999</ram:ID>");
        xml.Should().Contain("<ram:Name>SGrowing Enterprise Technologies SAS</ram:Name>");
        xml.Should().Contain("<ram:ID schemeID=\"VA\">FR88990011223</ram:ID>");
        xml.Should().Contain("<ram:Name>Airbus Operations SAS</ram:Name>");
        xml.Should().Contain("<ram:ID schemeID=\"VA\">FR12345678901</ram:ID>");
        xml.Should().Contain("<ram:GrandTotalAmount>12000.00</ram:GrandTotalAmount>");
        xml.Should().Contain("<ram:InvoiceCurrencyCode>EUR</ram:InvoiceCurrencyCode>");
    }
}
