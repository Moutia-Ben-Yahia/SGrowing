using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Billing;

public enum CommercialInvoiceStatus
{
    Draft = 1,
    Issued = 2,
    Paid = 3,
    PartiallyRefunded = 4,
    Reversed = 5
}

public record CurrencySpotRate(
    string Code,
    decimal SpotRateToUsd,
    decimal SpreadMarginPct,
    DateTime LastUpdated,
    string Symbol,
    string CurrencyName);

public record TaxJurisdictionRule(
    string CountryCode,
    decimal StandardRatePct,
    decimal ReducedRatePct,
    bool IsEuMember,
    bool IsGccMember,
    bool HasViesSupport,
    bool HasZatcaPhase2,
    string TaxName,
    string CountryName);

public class CommercialInvoiceLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public record TaxEvaluationRequest(
    string MerchantCountry,
    string CustomerCountry,
    bool IsB2B,
    string? CustomerVatNumber,
    decimal Subtotal,
    List<CommercialInvoiceLineItem>? LineItems = null);

public record TaxEvaluationResult(
    decimal ApplicableTaxRatePct,
    decimal TaxAmount,
    string TaxScheme,
    bool IsReverseCharge,
    string? ExemptionReason,
    bool ViesValid);

public class CommercialInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);
    public string Currency { get; set; } = "USD";
    public List<CommercialInvoiceLineItem> LineItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal TaxRatePct { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal CreditNotesApplied { get; set; }
    public CommercialInvoiceStatus Status { get; set; } = CommercialInvoiceStatus.Draft;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerCountry { get; set; } = string.Empty;
    public string? CustomerVatNumber { get; set; }
    public string MerchantCountry { get; set; } = "FR";
    public string? ZatcaQrBase64 { get; set; }
    public string? FacturXXmlPayload { get; set; }
}

public class CreditNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public Guid OriginalInvoiceId { get; set; }
    public string OriginalInvoiceNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public decimal TaxAdjustment { get; set; }
    public decimal TotalCredit { get; set; }
    public string Status { get; set; } = "Issued";
}

public record CreateCommercialInvoiceRequest(
    string CustomerName,
    string CustomerEmail,
    string CustomerCountry,
    string? CustomerVatNumber,
    bool IsB2B,
    string Currency,
    List<CommercialInvoiceLineItem> LineItems,
    string? MerchantCountry = "FR",
    int DueDays = 30);

public record IssueCreditNoteRequest(
    string Reason,
    decimal RefundAmount,
    bool IsGrossAmount = true);

public record TaxLiabilitySummaryDto(
    string Jurisdiction,
    string CountryName,
    string Currency,
    int InvoicesCount,
    decimal GrossSubtotal,
    decimal TotalTaxCollected,
    decimal TotalCreditsAdjusted,
    decimal NetTaxLiability);

public interface ITaxAndInvoiceLedgerService
{
    Task<TaxEvaluationResult> EvaluateTaxAsync(TaxEvaluationRequest request, CancellationToken ct = default);
    bool ValidateViesFormat(string countryCode, string? vatNumber);
    TaxJurisdictionRule? GetJurisdictionRule(string countryCode);
    List<TaxJurisdictionRule> GetAllJurisdictionRules();

    List<CurrencySpotRate> GetCurrencies();
    decimal CalculateEffectiveRate(decimal spotRate, decimal spreadMarginPct = 1.5m);
    decimal ConvertCurrency(decimal amount, string fromCurrency, string toCurrency);

    Task<List<CommercialInvoice>> GetInvoicesAsync(
        Guid tenantId,
        string? status = null,
        string? currency = null,
        string? customer = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default);

    Task<CommercialInvoice?> GetInvoiceByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<CommercialInvoice> IssueInvoiceAsync(Guid tenantId, CreateCommercialInvoiceRequest request, CancellationToken ct = default);

    Task<CreditNote> IssueCreditNoteAsync(Guid tenantId, Guid invoiceId, IssueCreditNoteRequest request, CancellationToken ct = default);
    Task<List<CreditNote>> GetCreditNotesAsync(Guid tenantId, Guid? invoiceId = null, CancellationToken ct = default);

    Task<List<TaxLiabilitySummaryDto>> GetTaxLiabilitySummaryAsync(Guid tenantId, CancellationToken ct = default);

    string GenerateZatcaQr(string sellerName, string sellerVatNumber, DateTime timestampUtc, decimal totalAmount, decimal vatAmount);
    string GenerateFacturXXml(CommercialInvoice invoice, string sellerName, string sellerVatNumber, string sellerCountry);
}

public class TaxAndInvoiceLedgerService : ITaxAndInvoiceLedgerService
{
    private static readonly ConcurrentDictionary<Guid, CommercialInvoice> _invoices = new();
    private static readonly ConcurrentDictionary<Guid, CreditNote> _creditNotes = new();
    private static int _invoiceCounter = 100;
    private static int _creditNoteCounter = 50;

    private readonly List<CurrencySpotRate> _currencies = new();
    private readonly Dictionary<string, TaxJurisdictionRule> _jurisdictions = new(StringComparer.OrdinalIgnoreCase);

    // EU VIES VAT regex formats
    private static readonly Dictionary<string, Regex> _viesRegexMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FR"] = new Regex(@"^FR[A-Z0-9]{2}\d{9}$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["DE"] = new Regex(@"^DE\d{9}$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["IT"] = new Regex(@"^IT\d{11}$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["ES"] = new Regex(@"^ES[A-Z0-9]\d{7}[A-Z0-9]$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["NL"] = new Regex(@"^NL\d{9}B\d{2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["BE"] = new Regex(@"^BE[01]\d{9}$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["AT"] = new Regex(@"^ATU\d{8}$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["IE"] = new Regex(@"^IE\d{7}[A-W][A-I]?$|^IE[0-9A-Z+*]{8,9}$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    public TaxAndInvoiceLedgerService()
    {
        InitializeCurrencies();
        InitializeJurisdictions();
        SeedDefaultLedgerData();
    }

    private void InitializeCurrencies()
    {
        _currencies.AddRange(new[]
        {
            new CurrencySpotRate("USD", 1.0m, 0.0m, DateTime.UtcNow, "$", "US Dollar"),
            new CurrencySpotRate("EUR", 0.92m, 1.5m, DateTime.UtcNow, "€", "Euro"),
            new CurrencySpotRate("GBP", 0.79m, 1.5m, DateTime.UtcNow, "£", "British Pound"),
            new CurrencySpotRate("SAR", 3.75m, 1.2m, DateTime.UtcNow, "﷼", "Saudi Riyal"),
            new CurrencySpotRate("AED", 3.67m, 1.2m, DateTime.UtcNow, "د.إ", "UAE Dirham"),
            new CurrencySpotRate("TND", 3.12m, 2.0m, DateTime.UtcNow, "DT", "Tunisian Dinar")
        });
    }

    private void InitializeJurisdictions()
    {
        var rules = new[]
        {
            new TaxJurisdictionRule("FR", 20.0m, 5.5m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "TVA", CountryName: "France"),
            new TaxJurisdictionRule("DE", 19.0m, 7.0m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "MwSt", CountryName: "Germany"),
            new TaxJurisdictionRule("IT", 22.0m, 10.0m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "IVA", CountryName: "Italy"),
            new TaxJurisdictionRule("ES", 21.0m, 10.0m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "IVA", CountryName: "Spain"),
            new TaxJurisdictionRule("NL", 21.0m, 9.0m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "BTW", CountryName: "Netherlands"),
            new TaxJurisdictionRule("BE", 21.0m, 6.0m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "TVA", CountryName: "Belgium"),
            new TaxJurisdictionRule("AT", 20.0m, 10.0m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "USt", CountryName: "Austria"),
            new TaxJurisdictionRule("IE", 23.0m, 13.5m, IsEuMember: true, IsGccMember: false, HasViesSupport: true, HasZatcaPhase2: false, TaxName: "VAT", CountryName: "Ireland"),
            new TaxJurisdictionRule("SA", 15.0m, 0.0m, IsEuMember: false, IsGccMember: true, HasViesSupport: false, HasZatcaPhase2: true, TaxName: "ZATCA VAT", CountryName: "Saudi Arabia"),
            new TaxJurisdictionRule("AE", 5.0m, 0.0m, IsEuMember: false, IsGccMember: true, HasViesSupport: false, HasZatcaPhase2: false, TaxName: "FTA VAT", CountryName: "United Arab Emirates"),
            new TaxJurisdictionRule("TN", 19.0m, 7.0m, IsEuMember: false, IsGccMember: false, HasViesSupport: false, HasZatcaPhase2: false, TaxName: "TVA", CountryName: "Tunisia"),
            new TaxJurisdictionRule("GB", 20.0m, 5.0m, IsEuMember: false, IsGccMember: false, HasViesSupport: false, HasZatcaPhase2: false, TaxName: "VAT", CountryName: "United Kingdom"),
            new TaxJurisdictionRule("US", 0.0m, 0.0m, IsEuMember: false, IsGccMember: false, HasViesSupport: false, HasZatcaPhase2: false, TaxName: "Sales Tax", CountryName: "United States")
        };

        foreach (var rule in rules)
        {
            _jurisdictions[rule.CountryCode] = rule;
        }
    }

    public List<CurrencySpotRate> GetCurrencies() => _currencies;

    public TaxJurisdictionRule? GetJurisdictionRule(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        _jurisdictions.TryGetValue(countryCode.Trim(), out var rule);
        return rule;
    }

    public List<TaxJurisdictionRule> GetAllJurisdictionRules() => _jurisdictions.Values.ToList();

    public decimal CalculateEffectiveRate(decimal spotRate, decimal spreadMarginPct = 1.5m)
    {
        return Math.Round(spotRate * (1.0m + (spreadMarginPct / 100.0m)), 6);
    }

    public decimal ConvertCurrency(decimal amount, string fromCurrency, string toCurrency)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;

        var from = _currencies.FirstOrDefault(c => string.Equals(c.Code, fromCurrency, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ArgumentException($"Unsupported currency '{fromCurrency}'");
        var to = _currencies.FirstOrDefault(c => string.Equals(c.Code, toCurrency, StringComparison.OrdinalIgnoreCase))
                 ?? throw new ArgumentException($"Unsupported currency '{toCurrency}'");

        decimal fromEffective = CalculateEffectiveRate(from.SpotRateToUsd, from.SpreadMarginPct);
        decimal toEffective = CalculateEffectiveRate(to.SpotRateToUsd, to.SpreadMarginPct);

        // Convert from source to USD base
        decimal amountInUsd = string.Equals(from.Code, "USD", StringComparison.OrdinalIgnoreCase)
            ? amount
            : amount / fromEffective;

        // Convert from USD base to target
        decimal converted = string.Equals(to.Code, "USD", StringComparison.OrdinalIgnoreCase)
            ? amountInUsd
            : amountInUsd * toEffective;

        return Math.Round(converted, 2);
    }

    public bool ValidateViesFormat(string countryCode, string? vatNumber)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(vatNumber))
            return false;

        string code = countryCode.Trim().ToUpperInvariant();
        string cleanVat = Regex.Replace(vatNumber.Trim().ToUpperInvariant(), @"[\s\.-]", "");

        // If the VAT number doesn't start with the country code, prepend it
        if (!cleanVat.StartsWith(code, StringComparison.OrdinalIgnoreCase))
        {
            cleanVat = code + cleanVat;
        }

        if (_viesRegexMap.TryGetValue(code, out var regex))
        {
            return regex.IsMatch(cleanVat);
        }

        // Generic fallback for EU countries without specific regex: 2-letter country code + 8-12 alphanumeric
        return Regex.IsMatch(cleanVat, $@"^{code}[A-Z0-9]{{8,12}}$", RegexOptions.IgnoreCase);
    }

    public Task<TaxEvaluationResult> EvaluateTaxAsync(TaxEvaluationRequest request, CancellationToken ct = default)
    {
        string merchantCode = string.IsNullOrWhiteSpace(request.MerchantCountry) ? "FR" : request.MerchantCountry.Trim().ToUpperInvariant();
        string customerCode = string.IsNullOrWhiteSpace(request.CustomerCountry) ? "FR" : request.CustomerCountry.Trim().ToUpperInvariant();

        var merchantRule = GetJurisdictionRule(merchantCode);
        var customerRule = GetJurisdictionRule(customerCode);

        bool isMerchantEu = merchantRule?.IsEuMember ?? false;
        bool isCustomerEu = customerRule?.IsEuMember ?? false;

        decimal subtotal = request.Subtotal;

        // 1. Cross-Border EU or Domestic EU Evaluation
        if (isMerchantEu && isCustomerEu)
        {
            bool isDomestic = string.Equals(merchantCode, customerCode, StringComparison.OrdinalIgnoreCase);

            if (isDomestic)
            {
                decimal rate = merchantRule!.StandardRatePct;
                decimal tax = Math.Round(subtotal * (rate / 100.0m), 2);
                return Task.FromResult(new TaxEvaluationResult(
                    ApplicableTaxRatePct: rate,
                    TaxAmount: tax,
                    TaxScheme: $"Domestic EU {merchantRule.TaxName}",
                    IsReverseCharge: false,
                    ExemptionReason: null,
                    ViesValid: ValidateViesFormat(customerCode, request.CustomerVatNumber)
                ));
            }
            else
            {
                // Cross-border EU
                bool viesValid = ValidateViesFormat(customerCode, request.CustomerVatNumber);
                if (request.IsB2B && viesValid)
                {
                    // Reverse Charge applies under Art. 196 EU VAT Directive
                    return Task.FromResult(new TaxEvaluationResult(
                        ApplicableTaxRatePct: 0.0m,
                        TaxAmount: 0.0m,
                        TaxScheme: "EU Cross-Border B2B Reverse Charge",
                        IsReverseCharge: true,
                        ExemptionReason: "Reverse charge: Art. 196 EU VAT Directive",
                        ViesValid: true
                    ));
                }
                else
                {
                    // B2C or invalid VIES: Destination VAT (OSS)
                    decimal rate = customerRule?.StandardRatePct ?? merchantRule!.StandardRatePct;
                    decimal tax = Math.Round(subtotal * (rate / 100.0m), 2);
                    return Task.FromResult(new TaxEvaluationResult(
                        ApplicableTaxRatePct: rate,
                        TaxAmount: tax,
                        TaxScheme: $"EU B2C Destination {customerRule?.TaxName ?? "VAT"}",
                        IsReverseCharge: false,
                        ExemptionReason: null,
                        ViesValid: viesValid
                    ));
                }
            }
        }

        // 2. Saudi Arabia (ZATCA Phase 2)
        if (string.Equals(customerCode, "SA", StringComparison.OrdinalIgnoreCase))
        {
            decimal rate = 15.0m;
            decimal tax = Math.Round(subtotal * (rate / 100.0m), 2);
            return Task.FromResult(new TaxEvaluationResult(
                ApplicableTaxRatePct: rate,
                TaxAmount: tax,
                TaxScheme: "KSA ZATCA VAT",
                IsReverseCharge: false,
                ExemptionReason: null,
                ViesValid: false
            ));
        }

        // 3. UAE (FTA)
        if (string.Equals(customerCode, "AE", StringComparison.OrdinalIgnoreCase))
        {
            decimal rate = 5.0m;
            decimal tax = Math.Round(subtotal * (rate / 100.0m), 2);
            return Task.FromResult(new TaxEvaluationResult(
                ApplicableTaxRatePct: rate,
                TaxAmount: tax,
                TaxScheme: "UAE FTA VAT",
                IsReverseCharge: false,
                ExemptionReason: null,
                ViesValid: false
            ));
        }

        // 4. Tunisia Domestic
        if (string.Equals(customerCode, "TN", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(merchantCode, "TN", StringComparison.OrdinalIgnoreCase))
            {
                decimal rate = 19.0m;
                decimal tax = Math.Round(subtotal * (rate / 100.0m), 2);
                return Task.FromResult(new TaxEvaluationResult(
                    ApplicableTaxRatePct: rate,
                    TaxAmount: tax,
                    TaxScheme: "Tunisian Domestic VAT",
                    IsReverseCharge: false,
                    ExemptionReason: null,
                    ViesValid: false
                ));
            }
            else
            {
                // Export to Tunisia
                return Task.FromResult(new TaxEvaluationResult(
                    ApplicableTaxRatePct: 0.0m,
                    TaxAmount: 0.0m,
                    TaxScheme: "Zero-Rated International Export",
                    IsReverseCharge: false,
                    ExemptionReason: "Export of services outside tax territory",
                    ViesValid: false
                ));
            }
        }

        // 5. United Kingdom
        if (string.Equals(customerCode, "GB", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(merchantCode, "GB", StringComparison.OrdinalIgnoreCase))
            {
                decimal rate = 20.0m;
                decimal tax = Math.Round(subtotal * (rate / 100.0m), 2);
                return Task.FromResult(new TaxEvaluationResult(
                    ApplicableTaxRatePct: rate,
                    TaxAmount: tax,
                    TaxScheme: "UK Domestic VAT",
                    IsReverseCharge: false,
                    ExemptionReason: null,
                    ViesValid: false
                ));
            }
        }

        // 6. Non-resident / International Export Zero-Rated (e.g. US, ROW)
        return Task.FromResult(new TaxEvaluationResult(
            ApplicableTaxRatePct: 0.0m,
            TaxAmount: 0.0m,
            TaxScheme: "Zero-Rated International Export",
            IsReverseCharge: false,
            ExemptionReason: "Export of services outside merchant tax jurisdiction",
            ViesValid: false
        ));
    }

    public string GenerateZatcaQr(string sellerName, string sellerVatNumber, DateTime timestampUtc, decimal totalAmount, decimal vatAmount)
    {
        using var ms = new MemoryStream();

        void WriteTlv(byte tag, string value)
        {
            var valBytes = Encoding.UTF8.GetBytes(value);
            ms.WriteByte(tag);
            ms.WriteByte((byte)valBytes.Length);
            ms.Write(valBytes, 0, valBytes.Length);
        }

        WriteTlv(1, sellerName);
        WriteTlv(2, sellerVatNumber);
        WriteTlv(3, timestampUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        WriteTlv(4, totalAmount.ToString("0.00", CultureInfo.InvariantCulture));
        WriteTlv(5, vatAmount.ToString("0.00", CultureInfo.InvariantCulture));

        return Convert.ToBase64String(ms.ToArray());
    }

    public static Dictionary<int, string> DecodeZatcaQr(string base64)
    {
        var result = new Dictionary<int, string>();
        byte[] bytes = Convert.FromBase64String(base64);
        int idx = 0;
        while (idx + 2 <= bytes.Length)
        {
            int tag = bytes[idx++];
            int len = bytes[idx++];
            if (idx + len > bytes.Length) break;
            string val = Encoding.UTF8.GetString(bytes, idx, len);
            result[tag] = val;
            idx += len;
        }
        return result;
    }

    public string GenerateFacturXXml(CommercialInvoice invoice, string sellerName, string sellerVatNumber, string sellerCountry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"");
        sb.AppendLine("                          xmlns:ram=\"urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100\"");
        sb.AppendLine("                          xmlns:udt=\"urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100\">");
        sb.AppendLine("  <rsm:ExchangedDocumentContext>");
        sb.AppendLine("    <ram:GuidelineSpecifiedDocumentContextParameter>");
        sb.AppendLine("      <ram:ID>urn:cen.eu:en16931:2017#compliant#urn:factur-x.eu:1p0:basic</ram:ID>");
        sb.AppendLine("    </ram:GuidelineSpecifiedDocumentContextParameter>");
        sb.AppendLine("  </rsm:ExchangedDocumentContext>");
        sb.AppendLine("  <rsm:ExchangedDocument>");
        sb.AppendLine($"    <ram:ID>{invoice.InvoiceNumber}</ram:ID>");
        sb.AppendLine("    <ram:TypeCode>380</ram:TypeCode>");
        sb.AppendLine("    <ram:IssueDateTime>");
        sb.AppendLine($"      <udt:DateTimeString format=\"102\">{invoice.IssueDate:yyyyMMdd}</udt:DateTimeString>");
        sb.AppendLine("    </ram:IssueDateTime>");
        sb.AppendLine("  </rsm:ExchangedDocument>");
        sb.AppendLine("  <rsm:SupplyChainTradeTransaction>");
        sb.AppendLine("    <ram:ApplicableHeaderTradeAgreement>");
        sb.AppendLine("      <ram:SellerTradeParty>");
        sb.AppendLine($"        <ram:Name>{sellerName}</ram:Name>");
        sb.AppendLine("        <ram:SpecifiedTaxRegistration>");
        sb.AppendLine($"          <ram:ID schemeID=\"VA\">{sellerVatNumber}</ram:ID>");
        sb.AppendLine("        </ram:SpecifiedTaxRegistration>");
        sb.AppendLine("        <ram:PostalTradeAddress>");
        sb.AppendLine($"          <ram:CountryID>{sellerCountry}</ram:CountryID>");
        sb.AppendLine("        </ram:PostalTradeAddress>");
        sb.AppendLine("      </ram:SellerTradeParty>");
        sb.AppendLine("      <ram:BuyerTradeParty>");
        sb.AppendLine($"        <ram:Name>{invoice.CustomerName}</ram:Name>");
        sb.AppendLine("        <ram:SpecifiedTaxRegistration>");
        sb.AppendLine($"          <ram:ID schemeID=\"VA\">{invoice.CustomerVatNumber ?? "NOT-APPLICABLE"}</ram:ID>");
        sb.AppendLine("        </ram:SpecifiedTaxRegistration>");
        sb.AppendLine("        <ram:PostalTradeAddress>");
        sb.AppendLine($"          <ram:CountryID>{invoice.CustomerCountry}</ram:CountryID>");
        sb.AppendLine("        </ram:PostalTradeAddress>");
        sb.AppendLine("      </ram:BuyerTradeParty>");
        sb.AppendLine("    </ram:ApplicableHeaderTradeAgreement>");
        sb.AppendLine("    <ram:ApplicableHeaderTradeSettlement>");
        sb.AppendLine($"      <ram:InvoiceCurrencyCode>{invoice.Currency}</ram:InvoiceCurrencyCode>");
        sb.AppendLine("      <ram:SpecifiedTradeSettlementHeaderMonetarySummation>");
        sb.AppendLine($"        <ram:LineTotalAmount>{invoice.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)}</ram:LineTotalAmount>");
        sb.AppendLine($"        <ram:TaxBasisTotalAmount>{invoice.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)}</ram:TaxBasisTotalAmount>");
        sb.AppendLine($"        <ram:TaxTotalAmount currencyID=\"{invoice.Currency}\">{invoice.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture)}</ram:TaxTotalAmount>");
        sb.AppendLine($"        <ram:GrandTotalAmount>{invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)}</ram:GrandTotalAmount>");
        sb.AppendLine($"        <ram:DuePayableAmount>{invoice.RemainingBalance.ToString("0.00", CultureInfo.InvariantCulture)}</ram:DuePayableAmount>");
        sb.AppendLine("      </ram:SpecifiedTradeSettlementHeaderMonetarySummation>");
        sb.AppendLine("    </ram:ApplicableHeaderTradeSettlement>");
        sb.AppendLine("  </rsm:SupplyChainTradeTransaction>");
        sb.AppendLine("</rsm:CrossIndustryInvoice>");
        return sb.ToString();
    }

    public Task<List<CommercialInvoice>> GetInvoicesAsync(
        Guid tenantId,
        string? status = null,
        string? currency = null,
        string? customer = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var query = _invoices.Values.Where(inv => inv.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CommercialInvoiceStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(inv => inv.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            query = query.Where(inv => string.Equals(inv.Currency, currency, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            query = query.Where(inv => inv.CustomerName.Contains(customer, StringComparison.OrdinalIgnoreCase) ||
                                       inv.CustomerEmail.Contains(customer, StringComparison.OrdinalIgnoreCase));
        }

        if (startDate.HasValue)
        {
            query = query.Where(inv => inv.IssueDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(inv => inv.IssueDate <= endDate.Value);
        }

        return Task.FromResult(query.OrderByDescending(inv => inv.IssueDate).ToList());
    }

    public Task<CommercialInvoice?> GetInvoiceByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        _invoices.TryGetValue(id, out var invoice);
        if (invoice != null && invoice.TenantId == tenantId)
            return Task.FromResult<CommercialInvoice?>(invoice);

        return Task.FromResult<CommercialInvoice?>(null);
    }

    public async Task<CommercialInvoice> IssueInvoiceAsync(Guid tenantId, CreateCommercialInvoiceRequest request, CancellationToken ct = default)
    {
        string merchantCountry = string.IsNullOrWhiteSpace(request.MerchantCountry) ? "FR" : request.MerchantCountry.Trim().ToUpperInvariant();
        string customerCountry = string.IsNullOrWhiteSpace(request.CustomerCountry) ? "FR" : request.CustomerCountry.Trim().ToUpperInvariant();

        decimal subtotal = request.LineItems.Sum(li => li.TotalPrice > 0 ? li.TotalPrice : li.Quantity * li.UnitPrice);

        var taxEval = await EvaluateTaxAsync(new TaxEvaluationRequest(
            MerchantCountry: merchantCountry,
            CustomerCountry: customerCountry,
            IsB2B: request.IsB2B,
            CustomerVatNumber: request.CustomerVatNumber,
            Subtotal: subtotal,
            LineItems: request.LineItems
        ), ct);

        decimal taxRatePct = taxEval.ApplicableTaxRatePct;
        decimal taxAmount = taxEval.TaxAmount;
        decimal totalAmount = subtotal + taxAmount;

        int seq = Interlocked.Increment(ref _invoiceCounter);
        string invoiceNumber = $"INV-2026-{seq:D5}";

        DateTime now = DateTime.UtcNow;
        var invoice = new CommercialInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            IssueDate = now,
            DueDate = now.AddDays(request.DueDays > 0 ? request.DueDays : 30),
            Currency = request.Currency.ToUpperInvariant(),
            LineItems = request.LineItems,
            Subtotal = subtotal,
            TaxRatePct = taxRatePct,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            RemainingBalance = totalAmount,
            CreditNotesApplied = 0.0m,
            Status = CommercialInvoiceStatus.Issued,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            CustomerCountry = customerCountry,
            CustomerVatNumber = request.CustomerVatNumber,
            MerchantCountry = merchantCountry
        };

        // If Saudi Arabia, generate ZATCA Phase 2 QR Base64
        if (string.Equals(customerCountry, "SA", StringComparison.OrdinalIgnoreCase))
        {
            invoice.ZatcaQrBase64 = GenerateZatcaQr(
                sellerName: "SGrowing Saudi Arabia Ltd",
                sellerVatNumber: "310123456700003",
                timestampUtc: now,
                totalAmount: totalAmount,
                vatAmount: taxAmount
            );
        }

        // Generate Factur-X standard XML
        invoice.FacturXXmlPayload = GenerateFacturXXml(
            invoice: invoice,
            sellerName: "SGrowing Enterprise Technologies SAS",
            sellerVatNumber: "FR88990011223",
            sellerCountry: merchantCountry
        );

        _invoices[invoice.Id] = invoice;
        return invoice;
    }

    public Task<CreditNote> IssueCreditNoteAsync(Guid tenantId, Guid invoiceId, IssueCreditNoteRequest request, CancellationToken ct = default)
    {
        if (!_invoices.TryGetValue(invoiceId, out var invoice) || invoice.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Commercial Invoice '{invoiceId}' not found for tenant '{tenantId}'.");
        }

        if (invoice.Status == CommercialInvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Cannot issue credit note against a draft invoice.");
        }

        if (invoice.Status == CommercialInvoiceStatus.Reversed)
        {
            throw new InvalidOperationException("Invoice has already been fully reversed with credit notes.");
        }

        decimal requestedCredit = request.RefundAmount;
        if (requestedCredit <= 0)
        {
            throw new ArgumentException("Refund amount must be greater than zero.");
        }

        decimal baseRefund;
        decimal taxAdjustment;
        decimal totalCredit;

        if (request.IsGrossAmount)
        {
            totalCredit = requestedCredit;
            if (invoice.TaxRatePct > 0)
            {
                // Proportionate tax extraction: Tax = Gross * (Rate / (100 + Rate))
                taxAdjustment = Math.Round(totalCredit * (invoice.TaxRatePct / (100.0m + invoice.TaxRatePct)), 2);
                baseRefund = totalCredit - taxAdjustment;
            }
            else
            {
                taxAdjustment = 0.0m;
                baseRefund = totalCredit;
            }
        }
        else
        {
            baseRefund = requestedCredit;
            taxAdjustment = invoice.TaxRatePct > 0 ? Math.Round(baseRefund * (invoice.TaxRatePct / 100.0m), 2) : 0.0m;
            totalCredit = baseRefund + taxAdjustment;
        }

        if (totalCredit > invoice.RemainingBalance + 0.001m)
        {
            throw new InvalidOperationException(
                $"Requested credit of {totalCredit:F2} {invoice.Currency} exceeds remaining balance of {invoice.RemainingBalance:F2} {invoice.Currency}.");
        }

        // Sequential Credit Note Numbering
        int seq = Interlocked.Increment(ref _creditNoteCounter);
        string cnNumber = $"CN-2026-{seq:D5}";

        var creditNote = new CreditNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreditNoteNumber = cnNumber,
            OriginalInvoiceId = invoice.Id,
            OriginalInvoiceNumber = invoice.InvoiceNumber,
            IssueDate = DateTime.UtcNow,
            Reason = request.Reason,
            RefundAmount = baseRefund,
            TaxAdjustment = taxAdjustment,
            TotalCredit = totalCredit,
            Status = "AppliedToLedger"
        };

        // Reduce remaining balance and update status
        invoice.CreditNotesApplied += totalCredit;
        invoice.RemainingBalance -= totalCredit;

        if (invoice.RemainingBalance <= 0.001m)
        {
            invoice.RemainingBalance = 0m;
            invoice.Status = CommercialInvoiceStatus.Reversed;
        }
        else
        {
            invoice.Status = CommercialInvoiceStatus.PartiallyRefunded;
        }

        _creditNotes[creditNote.Id] = creditNote;
        return Task.FromResult(creditNote);
    }

    public Task<List<CreditNote>> GetCreditNotesAsync(Guid tenantId, Guid? invoiceId = null, CancellationToken ct = default)
    {
        var query = _creditNotes.Values.Where(cn => cn.TenantId == tenantId);
        if (invoiceId.HasValue)
        {
            query = query.Where(cn => cn.OriginalInvoiceId == invoiceId.Value);
        }
        return Task.FromResult(query.OrderByDescending(cn => cn.IssueDate).ToList());
    }

    public Task<List<TaxLiabilitySummaryDto>> GetTaxLiabilitySummaryAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenantInvoices = _invoices.Values.Where(inv => inv.TenantId == tenantId).ToList();
        var tenantCreditNotes = _creditNotes.Values.Where(cn => cn.TenantId == tenantId).ToList();

        // Group by CustomerCountry and Currency
        var groups = tenantInvoices.GroupBy(inv => new { inv.CustomerCountry, inv.Currency });
        var summaries = new List<TaxLiabilitySummaryDto>();

        foreach (var group in groups)
        {
            string country = group.Key.CustomerCountry;
            string currency = group.Key.Currency;
            var rule = GetJurisdictionRule(country);

            var invoiceIds = group.Select(i => i.Id).ToHashSet();
            var relatedCreditNotes = tenantCreditNotes.Where(cn => invoiceIds.Contains(cn.OriginalInvoiceId)).ToList();

            decimal grossSubtotal = group.Sum(i => i.Subtotal);
            decimal totalTaxCollected = group.Sum(i => i.TaxAmount);
            decimal totalCreditsAdjusted = relatedCreditNotes.Sum(cn => cn.TaxAdjustment);
            decimal netTax = totalTaxCollected - totalCreditsAdjusted;

            summaries.Add(new TaxLiabilitySummaryDto(
                Jurisdiction: country,
                CountryName: rule?.CountryName ?? country,
                Currency: currency,
                InvoicesCount: group.Count(),
                GrossSubtotal: grossSubtotal,
                TotalTaxCollected: totalTaxCollected,
                TotalCreditsAdjusted: totalCreditsAdjusted,
                NetTaxLiability: netTax
            ));
        }

        return Task.FromResult(summaries.OrderByDescending(s => s.NetTaxLiability).ToList());
    }

    private void SeedDefaultLedgerData()
    {
        if (!_invoices.IsEmpty) return;

        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var baseDate = DateTime.UtcNow.AddDays(-14);

        // 1. France B2B cross-border (Reverse Charge)
        var inv1 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110001"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00001",
            IssueDate = baseDate.AddDays(1),
            DueDate = baseDate.AddDays(31),
            Currency = "EUR",
            Subtotal = 12500.00m,
            TaxRatePct = 0.0m,
            TaxAmount = 0.0m,
            TotalAmount = 12500.00m,
            RemainingBalance = 0.00m,
            CreditNotesApplied = 0.00m,
            Status = CommercialInvoiceStatus.Paid,
            CustomerName = "Doctolib SAS",
            CustomerEmail = "billing@doctolib.fr",
            CustomerCountry = "FR",
            CustomerVatNumber = "FR32123456789",
            MerchantCountry = "DE",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Enterprise Healthcare Telephony SLA", Quantity = 1, UnitPrice = 8500.00m, TotalPrice = 8500.00m },
                new() { Description = "Dedicated HIPAA/GDPR Cloud Enclave", Quantity = 1, UnitPrice = 4000.00m, TotalPrice = 4000.00m }
            }
        };
        inv1.FacturXXmlPayload = GenerateFacturXXml(inv1, "SGrowing Enterprise GmbH", "DE998877665", "DE");
        _invoices[inv1.Id] = inv1;

        // 2. Germany B2B cross-border (Reverse Charge)
        var inv2 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110002"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00002",
            IssueDate = baseDate.AddDays(3),
            DueDate = baseDate.AddDays(33),
            Currency = "EUR",
            Subtotal = 8400.00m,
            TaxRatePct = 0.0m,
            TaxAmount = 0.0m,
            TotalAmount = 8400.00m,
            RemainingBalance = 8400.00m,
            CreditNotesApplied = 0.00m,
            Status = CommercialInvoiceStatus.Issued,
            CustomerName = "Siemens AG",
            CustomerEmail = "ap-invoices@siemens.de",
            CustomerCountry = "DE",
            CustomerVatNumber = "DE123456789",
            MerchantCountry = "FR",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Autonomous Inbound Voice AI Pipeline", Quantity = 2, UnitPrice = 4200.00m, TotalPrice = 8400.00m }
            }
        };
        inv2.FacturXXmlPayload = GenerateFacturXXml(inv2, "SGrowing Enterprise Technologies SAS", "FR88990011223", "FR");
        _invoices[inv2.Id] = inv2;

        // 3. Saudi Arabia (15% ZATCA Phase 2 with QR)
        var inv3 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110003"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00003",
            IssueDate = baseDate.AddDays(5),
            DueDate = baseDate.AddDays(35),
            Currency = "SAR",
            Subtotal = 45000.00m,
            TaxRatePct = 15.0m,
            TaxAmount = 6750.00m,
            TotalAmount = 51750.00m,
            RemainingBalance = 51750.00m,
            CreditNotesApplied = 0.00m,
            Status = CommercialInvoiceStatus.Issued,
            CustomerName = "Al Rajhi Retail Logistics",
            CustomerEmail = "procurement@alrajhi-logistics.sa",
            CustomerCountry = "SA",
            CustomerVatNumber = "310123456700003",
            MerchantCountry = "SA",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Omnichannel WhatsApp Fulfillment Mesh", Quantity = 1, UnitPrice = 30000.00m, TotalPrice = 30000.00m },
                new() { Description = "High-Capacity Arabic Voice Agent Cluster", Quantity = 1, UnitPrice = 15000.00m, TotalPrice = 15000.00m }
            }
        };
        inv3.ZatcaQrBase64 = GenerateZatcaQr("SGrowing Saudi Arabia Ltd", "310123456700003", inv3.IssueDate, inv3.TotalAmount, inv3.TaxAmount);
        inv3.FacturXXmlPayload = GenerateFacturXXml(inv3, "SGrowing Saudi Arabia Ltd", "310123456700003", "SA");
        _invoices[inv3.Id] = inv3;

        // 4. UAE (5% FTA VAT)
        var inv4 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110004"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00004",
            IssueDate = baseDate.AddDays(7),
            DueDate = baseDate.AddDays(37),
            Currency = "AED",
            Subtotal = 32000.00m,
            TaxRatePct = 5.0m,
            TaxAmount = 1600.00m,
            TotalAmount = 33600.00m,
            RemainingBalance = 0.00m,
            CreditNotesApplied = 0.00m,
            Status = CommercialInvoiceStatus.Paid,
            CustomerName = "Emaar Properties PJSC",
            CustomerEmail = "finance@emaar.ae",
            CustomerCountry = "AE",
            CustomerVatNumber = "100234567800003",
            MerchantCountry = "AE",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Luxury Real Estate Lead Journey Suite", Quantity = 1, UnitPrice = 32000.00m, TotalPrice = 32000.00m }
            }
        };
        inv4.FacturXXmlPayload = GenerateFacturXXml(inv4, "SGrowing Middle East FZ-LLC", "100998877600003", "AE");
        _invoices[inv4.Id] = inv4;

        // 5. Tunisia (19% TVA with partial credit note)
        var inv5 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110005"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00005",
            IssueDate = baseDate.AddDays(8),
            DueDate = baseDate.AddDays(38),
            Currency = "TND",
            Subtotal = 18000.00m,
            TaxRatePct = 19.0m,
            TaxAmount = 3420.00m,
            TotalAmount = 21420.00m,
            RemainingBalance = 17850.00m,
            CreditNotesApplied = 3570.00m,
            Status = CommercialInvoiceStatus.PartiallyRefunded,
            CustomerName = "Sousse MedTech SARL",
            CustomerEmail = "comptabilite@sousse-medtech.tn",
            CustomerCountry = "TN",
            CustomerVatNumber = "1234567A/P/M/000",
            MerchantCountry = "TN",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Clinical Telehealth & Inbound SMS Dispatch", Quantity = 1, UnitPrice = 18000.00m, TotalPrice = 18000.00m }
            }
        };
        inv5.FacturXXmlPayload = GenerateFacturXXml(inv5, "SGrowing North Africa SARL", "9876543B/M/000", "TN");
        _invoices[inv5.Id] = inv5;

        // Pre-seeded partial Credit Note for inv5
        var cn1 = new CreditNote
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222220001"),
            TenantId = defaultTenantId,
            CreditNoteNumber = "CN-2026-00001",
            OriginalInvoiceId = inv5.Id,
            OriginalInvoiceNumber = inv5.InvoiceNumber,
            IssueDate = baseDate.AddDays(10),
            Reason = "SLA delivery latency credit on monthly cloud nodes",
            RefundAmount = 3000.00m,
            TaxAdjustment = 570.00m,
            TotalCredit = 3570.00m,
            Status = "AppliedToLedger"
        };
        _creditNotes[cn1.Id] = cn1;

        // 6. United States (0% Export of services)
        var inv6 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110006"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00006",
            IssueDate = baseDate.AddDays(11),
            DueDate = baseDate.AddDays(41),
            Currency = "USD",
            Subtotal = 24000.00m,
            TaxRatePct = 0.0m,
            TaxAmount = 0.0m,
            TotalAmount = 24000.00m,
            RemainingBalance = 0.00m,
            CreditNotesApplied = 0.00m,
            Status = CommercialInvoiceStatus.Paid,
            CustomerName = "Anthropic PBC",
            CustomerEmail = "finance@anthropic.com",
            CustomerCountry = "US",
            CustomerVatNumber = null,
            MerchantCountry = "FR",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Multi-Model AI Workflow Mesh Integration", Quantity = 2, UnitPrice = 12000.00m, TotalPrice = 24000.00m }
            }
        };
        inv6.FacturXXmlPayload = GenerateFacturXXml(inv6, "SGrowing Enterprise Technologies SAS", "FR88990011223", "FR");
        _invoices[inv6.Id] = inv6;

        // 7. United Kingdom (20% VAT)
        var inv7 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110007"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00007",
            IssueDate = baseDate.AddDays(12),
            DueDate = baseDate.AddDays(42),
            Currency = "GBP",
            Subtotal = 9500.00m,
            TaxRatePct = 20.0m,
            TaxAmount = 1900.00m,
            TotalAmount = 11400.00m,
            RemainingBalance = 11400.00m,
            CreditNotesApplied = 0.00m,
            Status = CommercialInvoiceStatus.Issued,
            CustomerName = "London FinTech Partners LLP",
            CustomerEmail = "accounts@londonfintech.co.uk",
            CustomerCountry = "GB",
            CustomerVatNumber = "GB987654321",
            MerchantCountry = "GB",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "FCA Compliant Voice Call Recording & Transcription", Quantity = 1, UnitPrice = 9500.00m, TotalPrice = 9500.00m }
            }
        };
        inv7.FacturXXmlPayload = GenerateFacturXXml(inv7, "SGrowing UK Operations Ltd", "GB112233445", "GB");
        _invoices[inv7.Id] = inv7;

        // 8. France Domestic (20% TVA - fully reversed with 100% Credit Note)
        var inv8 = new CommercialInvoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111110008"),
            TenantId = defaultTenantId,
            InvoiceNumber = "INV-2026-00008",
            IssueDate = baseDate.AddDays(13),
            DueDate = baseDate.AddDays(43),
            Currency = "EUR",
            Subtotal = 5000.00m,
            TaxRatePct = 20.0m,
            TaxAmount = 1000.00m,
            TotalAmount = 6000.00m,
            RemainingBalance = 0.00m,
            CreditNotesApplied = 6000.00m,
            Status = CommercialInvoiceStatus.Reversed,
            CustomerName = "Atelier Parisien SARL",
            CustomerEmail = "admin@atelier-paris.fr",
            CustomerCountry = "FR",
            CustomerVatNumber = "FR99988877766",
            MerchantCountry = "FR",
            LineItems = new List<CommercialInvoiceLineItem>
            {
                new() { Description = "Custom Workflow Proof of Concept", Quantity = 1, UnitPrice = 5000.00m, TotalPrice = 5000.00m }
            }
        };
        inv8.FacturXXmlPayload = GenerateFacturXXml(inv8, "SGrowing Enterprise Technologies SAS", "FR88990011223", "FR");
        _invoices[inv8.Id] = inv8;

        var cn2 = new CreditNote
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222220002"),
            TenantId = defaultTenantId,
            CreditNoteNumber = "CN-2026-00002",
            OriginalInvoiceId = inv8.Id,
            OriginalInvoiceNumber = inv8.InvoiceNumber,
            IssueDate = baseDate.AddDays(14),
            Reason = "Full contract cancellation and project rescission",
            RefundAmount = 5000.00m,
            TaxAdjustment = 1000.00m,
            TotalCredit = 6000.00m,
            Status = "AppliedToLedger"
        };
        _creditNotes[cn2.Id] = cn2;
    }
}

public static class TaxAndInvoiceLedgerServiceExtensions
{
    public static IServiceCollection AddTaxAndInvoiceLedger(this IServiceCollection services)
    {
        services.AddSingleton<ITaxAndInvoiceLedgerService, TaxAndInvoiceLedgerService>();
        return services;
    }
}
