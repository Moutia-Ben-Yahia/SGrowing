using SGrowing.Api.Modules.Billing.TaxCompliance;

namespace SGrowing.Tests.Billing;

public class TaxComplianceTests
{
    private readonly TaxComplianceEngine _engine = new();

    [Fact]
    public void GetLiveFxRates_ReturnsMajorCurrencyPairs()
    {
        var rates = _engine.GetLiveFxRates();

        Assert.NotEmpty(rates);
        Assert.Contains(rates, r => r.ToCurrency == "EUR");
        Assert.Contains(rates, r => r.ToCurrency == "GBP");
        Assert.Contains(rates, r => r.ToCurrency == "JPY");
    }

    [Fact]
    public void CalculateTax_WhenEuB2BWithVatId_AppliesReverseCharge()
    {
        var req = new TaxCalculationRequest(
            AmountBaseCurrency: 1000.0,
            Currency: "EUR",
            CustomerCountry: "DE",
            IsB2B: true,
            VatOrTaxId: "DE123456789"
        );

        var result = _engine.CalculateTax(req);

        Assert.True(result.ReverseChargeApplied);
        Assert.Equal(0.0, result.TaxRatePercent);
        Assert.Equal(0.0, result.TaxAmount);
        Assert.Equal("EU-VAT-REVERSE-CHARGE-ART194", result.TaxCode);
    }

    [Fact]
    public void CalculateTax_WhenB2CCustomer_AppliesStandardVatRate()
    {
        var req = new TaxCalculationRequest(
            AmountBaseCurrency: 1000.0,
            Currency: "USD",
            CustomerCountry: "DE",
            IsB2B: false,
            VatOrTaxId: null
        );

        var result = _engine.CalculateTax(req);

        Assert.False(result.ReverseChargeApplied);
        Assert.Equal(19.0, result.TaxRatePercent); // German standard VAT
        Assert.Equal(190.0, result.TaxAmount);
        Assert.Equal(1190.0, result.TotalAmount);
    }

    [Fact]
    public void ValidateVatNumber_ChecksCountryPrefix()
    {
        Assert.True(_engine.ValidateVatNumber("DE12345678", "DE"));
        Assert.False(_engine.ValidateVatNumber("FR12345678", "DE"));
        Assert.False(_engine.ValidateVatNumber("", "DE"));
    }

    [Fact]
    public void ListNexusJurisdictions_ReturnsActiveAndPendingNexusRecords()
    {
        var jurisdictions = _engine.ListNexusJurisdictions();

        Assert.NotEmpty(jurisdictions);
        Assert.Contains(jurisdictions, j => j.CountryCode == "US-CA" && j.HasNexusEstablished);
        Assert.Contains(jurisdictions, j => j.CountryCode == "EU-OSS" && j.VatOssRegistered);
        Assert.Contains(jurisdictions, j => j.CountryCode == "US-TX" && !j.HasNexusEstablished);
    }

    [Fact]
    public void CalculateTax_USCustomer_AppliesSalesTax()
    {
        var req = new TaxCalculationRequest(
            AmountBaseCurrency: 200.0,
            Currency: "USD",
            CustomerCountry: "US",
            IsB2B: false,
            VatOrTaxId: null
        );

        var result = _engine.CalculateTax(req);

        Assert.False(result.ReverseChargeApplied);
        Assert.Equal(8.5, result.TaxRatePercent); // US standard 8.5%
        Assert.Equal(17.0, result.TaxAmount);
        Assert.Equal(217.0, result.TotalAmount);
        Assert.Equal("STANDARD-VAT-US", result.TaxCode);
    }

    [Fact]
    public void CalculateTax_UnregisteredCountry_ReturnsZeroRatedExempt()
    {
        var req = new TaxCalculationRequest(
            AmountBaseCurrency: 500.0,
            Currency: "USD",
            CustomerCountry: "CH", // Switzerland not in StandardVatRates dictionary
            IsB2B: false,
            VatOrTaxId: null
        );

        var result = _engine.CalculateTax(req);

        Assert.False(result.ReverseChargeApplied);
        Assert.Equal(0.0, result.TaxRatePercent);
        Assert.Equal(0.0, result.TaxAmount);
        Assert.Equal(500.0, result.TotalAmount);
        Assert.Equal("ZERO-RATED-EXEMPT", result.TaxCode);
    }

    [Fact]
    public void CalculateTax_WithCurrencyConversion_AppliesHedgedFxRate()
    {
        // HedgedSettlementRate for USD->EUR is 0.920
        var req = new TaxCalculationRequest(
            AmountBaseCurrency: 1000.0,
            Currency: "EUR",
            CustomerCountry: "FR",
            IsB2B: false,
            VatOrTaxId: null
        );

        var result = _engine.CalculateTax(req);

        Assert.Equal(920.0, result.ConvertedAmount); // 1000 * 0.920
        Assert.Equal(20.0, result.TaxRatePercent); // France 20%
        Assert.Equal(184.0, result.TaxAmount); // 920 * 0.20
        Assert.Equal(1104.0, result.TotalAmount);
    }

    [Fact]
    public void ValidateVatNumber_ShortNumber_ReturnsFalse()
    {
        // Must be >= 8 characters even if prefix matches
        Assert.False(_engine.ValidateVatNumber("DE123", "DE"));
        Assert.False(_engine.ValidateVatNumber("FR1234", "FR"));
    }
}
