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
}
