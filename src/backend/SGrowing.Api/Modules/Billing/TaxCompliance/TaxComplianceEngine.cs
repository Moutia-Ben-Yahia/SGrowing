namespace SGrowing.Api.Modules.Billing.TaxCompliance;

public interface ITaxComplianceEngine
{
    TaxCalculationResult CalculateTax(TaxCalculationRequest request);
    List<CurrencyFxRate> GetLiveFxRates();
    List<TaxJurisdictionNexus> ListNexusJurisdictions();
    bool ValidateVatNumber(string vatNumber, string countryCode);
}

public class TaxComplianceEngine : ITaxComplianceEngine
{
    private static readonly Dictionary<string, double> StandardVatRates = new()
    {
        ["DE"] = 19.0, // Germany
        ["FR"] = 20.0, // France
        ["GB"] = 20.0, // UK
        ["NL"] = 21.0, // Netherlands
        ["US"] = 8.5,  // US Average Sales Tax
        ["SG"] = 9.0   // Singapore GST
    };

    public TaxCalculationResult CalculateTax(TaxCalculationRequest request)
    {
        var fxRate = GetFxRate("USD", request.Currency);
        var convertedBase = Math.Round(request.AmountBaseCurrency * fxRate, 2);

        // EU B2B reverse charge rule: if valid VAT ID provided in cross-border EU transaction
        var isEuCountry = new[] { "DE", "FR", "NL", "IE", "ES", "IT" }.Contains(request.CustomerCountry.ToUpperInvariant());
        var reverseCharge = request.IsB2B && isEuCountry && !string.IsNullOrWhiteSpace(request.VatOrTaxId);

        double taxRate = 0;
        if (!reverseCharge && StandardVatRates.TryGetValue(request.CustomerCountry.ToUpperInvariant(), out var rate))
        {
            taxRate = rate;
        }

        var taxAmount = Math.Round(convertedBase * (taxRate / 100.0), 2);
        var total = convertedBase + taxAmount;

        var taxCode = reverseCharge
            ? "EU-VAT-REVERSE-CHARGE-ART194"
            : taxRate > 0 ? $"STANDARD-VAT-{request.CustomerCountry.ToUpperInvariant()}" : "ZERO-RATED-EXEMPT";

        return new TaxCalculationResult(
            BaseAmount: request.AmountBaseCurrency,
            ConvertedAmount: convertedBase,
            TaxRatePercent: taxRate,
            TaxAmount: taxAmount,
            TotalAmount: total,
            ReverseChargeApplied: reverseCharge,
            TaxCode: taxCode
        );
    }

    public List<CurrencyFxRate> GetLiveFxRates()
    {
        var now = DateTime.UtcNow;
        return new List<CurrencyFxRate>
        {
            new("USD", "EUR", 0.925, 0.920, 0.5, now),
            new("USD", "GBP", 0.792, 0.788, 0.5, now),
            new("USD", "JPY", 154.20, 153.40, 0.5, now),
            new("USD", "SGD", 1.345, 1.338, 0.5, now)
        };
    }

    public List<TaxJurisdictionNexus> ListNexusJurisdictions()
    {
        return new List<TaxJurisdictionNexus>
        {
            new("US-CA", "California, USA", "Revenue ($500k)", 500000, 680000, HasNexusEstablished: true, VatOssRegistered: false),
            new("US-NY", "New York, USA", "Revenue ($100k)", 100000, 142000, HasNexusEstablished: true, VatOssRegistered: false),
            new("US-TX", "Texas, USA", "Revenue ($500k)", 500000, 310000, HasNexusEstablished: false, VatOssRegistered: false),
            new("EU-OSS", "European Union One-Stop Shop", "Revenue (€10k cross-border)", 10000, 420000, HasNexusEstablished: true, VatOssRegistered: true),
            new("GB-VAT", "United Kingdom HMRC", "Revenue (£90k threshold)", 90000, 185000, HasNexusEstablished: true, VatOssRegistered: false)
        };
    }

    public bool ValidateVatNumber(string vatNumber, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(vatNumber)) return false;
        var clean = vatNumber.Trim().ToUpperInvariant();
        return clean.StartsWith(countryCode.ToUpperInvariant()) && clean.Length >= 8;
    }

    private double GetFxRate(string from, string to)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return 1.0;
        var rates = GetLiveFxRates();
        var match = rates.FirstOrDefault(r => r.FromCurrency.Equals(from, StringComparison.OrdinalIgnoreCase) && r.ToCurrency.Equals(to, StringComparison.OrdinalIgnoreCase));
        return match?.HedgedSettlementRate ?? 1.0;
    }
}
