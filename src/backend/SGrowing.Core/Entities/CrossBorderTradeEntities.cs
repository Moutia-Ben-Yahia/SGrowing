namespace SGrowing.Core.Entities;

public class CountryTradeConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CountryCode { get; set; } = "TN"; // TN, DZ, MA, SA, AE, FR, US
    public string CountryName { get; set; } = "Tunisia";
    public string CurrencyCode { get; set; } = "TND";
    public string PhonePrefix { get; set; } = "+216";
    public int NationalPhoneNumberLength { get; set; } = 8;
    public decimal DefaultVatPercentage { get; set; } = 19.0m;
    public decimal StandardShippingFee { get; set; } = 8.0m;
    public bool SupportsCod { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public static CountryTradeConfig Create(
        Guid tenantId,
        string countryCode,
        string countryName,
        string currencyCode,
        string phonePrefix,
        int nationalPhoneLength,
        decimal vatPercent,
        decimal standardShippingFee,
        bool supportsCod = true)
    {
        return new CountryTradeConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            CountryName = countryName.Trim(),
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            PhonePrefix = phonePrefix.Trim(),
            NationalPhoneNumberLength = nationalPhoneLength,
            DefaultVatPercentage = vatPercent,
            StandardShippingFee = standardShippingFee,
            SupportsCod = supportsCod,
            IsActive = true
        };
    }
}

public class FxExchangeRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string BaseCurrency { get; set; } = "TND";
    public string TargetCurrency { get; set; } = "USD";
    public decimal Rate { get; set; } = 0.32m;
    public bool IsPegged { get; set; } = false;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public static FxExchangeRate Create(
        Guid tenantId,
        string baseCurrency,
        string targetCurrency,
        decimal rate,
        bool isPegged = false)
    {
        return new FxExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = baseCurrency.Trim().ToUpperInvariant(),
            TargetCurrency = targetCurrency.Trim().ToUpperInvariant(),
            Rate = rate,
            IsPegged = isPegged,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }
}

public class CustomsInvoiceItem
{
    public string HsTariffCode { get; set; } = "6109.10"; // Harmonized System Code (e.g. 6109.10 for cotton t-shirts)
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal DeclaredUnitValue { get; set; }
    public decimal DeclaredTotalValue { get; set; }
    public string CountryOfOrigin { get; set; } = "TN";
}

public class CustomsCommercialInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ExporterName { get; set; } = "SGrowing Global Merchant";
    public string ExporterCountry { get; set; } = "TN";
    public string DestinationCountry { get; set; } = "DZ";
    public string ConsigneeCustomerName { get; set; } = string.Empty;
    public string ConsigneePhoneE164 { get; set; } = string.Empty;
    public string ConsigneeAddress { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal TotalDeclaredCustomsValue { get; set; }
    public decimal TotalWeightKg { get; set; } = 1.0m;
    public string Incoterm { get; set; } = "DAP"; // Delivered At Place
    public string ItemsJson { get; set; } = "[]";
    public DateTime ClearanceIssuedAtUtc { get; set; } = DateTime.UtcNow;
}
