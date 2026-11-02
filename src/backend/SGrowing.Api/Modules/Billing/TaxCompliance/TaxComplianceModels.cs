namespace SGrowing.Api.Modules.Billing.TaxCompliance;

public record CurrencyFxRate(
    string FromCurrency,
    string ToCurrency,
    double SpotRate,
    double HedgedSettlementRate,
    double SlippageBufferPercent,
    DateTime LastRefreshedUtc
);

public record TaxJurisdictionNexus(
    string CountryCode,
    string JurisdictionName,
    string ThresholdType, // "Revenue ($100k)", "Transactions (200)"
    double ThresholdLimit,
    double CurrentValue,
    bool HasNexusEstablished,
    bool VatOssRegistered
);

public record TaxCalculationRequest(
    double AmountBaseCurrency,
    string Currency, // "USD", "EUR", "GBP"
    string CustomerCountry,
    bool IsB2B,
    string? VatOrTaxId
);

public record TaxCalculationResult(
    double BaseAmount,
    double ConvertedAmount,
    double TaxRatePercent,
    double TaxAmount,
    double TotalAmount,
    bool ReverseChargeApplied,
    string TaxCode
);
