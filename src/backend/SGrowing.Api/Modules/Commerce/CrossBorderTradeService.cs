using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface ICrossBorderTradeService
{
    Task<CountryTradeConfig> ConfigureCountryAsync(Guid tenantId, CountryTradeConfig config);
    Task<CountryTradeConfig?> GetCountryConfigAsync(Guid tenantId, string countryCode);
    Task<List<CountryTradeConfig>> ListSupportedCountriesAsync(Guid tenantId);
    Task<FxExchangeRate> SetFxRateAsync(Guid tenantId, string baseCurrency, string targetCurrency, decimal rate, bool isPegged = false);
    Task<decimal> ConvertCurrencyAsync(Guid tenantId, decimal amount, string fromCurrency, string toCurrency);
    string NormalizePhoneNumber(string rawPhone, string countryCode);
    Task<CustomsCommercialInvoice> GenerateCustomsCommercialInvoiceAsync(
        Guid tenantId,
        Guid orderId,
        string destinationCountry,
        List<CustomsInvoiceItem> items,
        string currency = "EUR");
    Task<List<CustomsCommercialInvoice>> ListCommercialInvoicesAsync(Guid tenantId);
}

public class CrossBorderTradeService : ICrossBorderTradeService
{
    private readonly SGrowingDbContext _db;

    public CrossBorderTradeService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CountryTradeConfig> ConfigureCountryAsync(Guid tenantId, CountryTradeConfig config)
    {
        config.TenantId = tenantId;
        config.CountryCode = config.CountryCode.Trim().ToUpperInvariant();

        var existing = await _db.CountryTradeConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CountryCode == config.CountryCode);

        if (existing != null)
        {
            existing.CountryName = config.CountryName;
            existing.CurrencyCode = config.CurrencyCode.ToUpperInvariant();
            existing.PhonePrefix = config.PhonePrefix;
            existing.NationalPhoneNumberLength = config.NationalPhoneNumberLength;
            existing.DefaultVatPercentage = config.DefaultVatPercentage;
            existing.StandardShippingFee = config.StandardShippingFee;
            existing.SupportsCod = config.SupportsCod;
            existing.IsActive = config.IsActive;
            await _db.SaveChangesAsync();
            return existing;
        }

        _db.CountryTradeConfigs.Add(config);
        await _db.SaveChangesAsync();
        return config;
    }

    public async Task<CountryTradeConfig?> GetCountryConfigAsync(Guid tenantId, string countryCode)
    {
        var code = countryCode.Trim().ToUpperInvariant();
        return await _db.CountryTradeConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CountryCode == code);
    }

    public async Task<List<CountryTradeConfig>> ListSupportedCountriesAsync(Guid tenantId)
    {
        return await _db.CountryTradeConfigs
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.CountryName)
            .ToListAsync();
    }

    public async Task<FxExchangeRate> SetFxRateAsync(
        Guid tenantId,
        string baseCurrency,
        string targetCurrency,
        decimal rate,
        bool isPegged = false)
    {
        var bCurr = baseCurrency.Trim().ToUpperInvariant();
        var tCurr = targetCurrency.Trim().ToUpperInvariant();

        var existing = await _db.FxExchangeRates
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.BaseCurrency == bCurr && r.TargetCurrency == tCurr);

        if (existing != null)
        {
            existing.Rate = rate;
            existing.IsPegged = isPegged;
            existing.LastUpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        var fx = FxExchangeRate.Create(tenantId, bCurr, tCurr, rate, isPegged);
        _db.FxExchangeRates.Add(fx);
        await _db.SaveChangesAsync();
        return fx;
    }

    public async Task<decimal> ConvertCurrencyAsync(Guid tenantId, decimal amount, string fromCurrency, string toCurrency)
    {
        var from = fromCurrency.Trim().ToUpperInvariant();
        var to = toCurrency.Trim().ToUpperInvariant();

        if (from == to) return amount;

        // 1. Direct rate
        var direct = await _db.FxExchangeRates
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.BaseCurrency == from && r.TargetCurrency == to);

        if (direct != null && direct.Rate > 0)
        {
            return Math.Round(amount * direct.Rate, 2);
        }

        // 2. Inverse rate
        var inverse = await _db.FxExchangeRates
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.BaseCurrency == to && r.TargetCurrency == from);

        if (inverse != null && inverse.Rate > 0)
        {
            return Math.Round(amount / inverse.Rate, 2);
        }

        throw new InvalidOperationException($"No exchange rate configured from '{from}' to '{to}'.");
    }

    public string NormalizePhoneNumber(string rawPhone, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(rawPhone)) return string.Empty;

        // Strip non-digit characters except leading '+'
        bool hasPlus = rawPhone.TrimStart().StartsWith("+");
        string digitsOnly = Regex.Replace(rawPhone, @"\D", "");
        var country = countryCode.Trim().ToUpperInvariant();

        return country switch
        {
            "TN" => FormatWithPrefix(digitsOnly, "216", 8),
            "DZ" => FormatWithPrefix(digitsOnly, "213", 9),
            "MA" => FormatWithPrefix(digitsOnly, "212", 9),
            "SA" => FormatWithPrefix(digitsOnly, "966", 9),
            "AE" => FormatWithPrefix(digitsOnly, "971", 9),
            "FR" => FormatWithPrefix(digitsOnly, "33", 9),
            _ => hasPlus ? $"+{digitsOnly}" : digitsOnly
        };
    }

    private static string FormatWithPrefix(string digits, string countryPrefix, int nationalLength)
    {
        if (digits.StartsWith(countryPrefix))
        {
            return $"+{digits}";
        }

        // Remove leading trunk zero (e.g. 0550123456 -> 550123456)
        if (digits.StartsWith("0"))
        {
            digits = digits[1..];
        }

        if (digits.Length == nationalLength)
        {
            return $"+{countryPrefix}{digits}";
        }

        return $"+{countryPrefix}{digits}";
    }

    public async Task<CustomsCommercialInvoice> GenerateCustomsCommercialInvoiceAsync(
        Guid tenantId,
        Guid orderId,
        string destinationCountry,
        List<CustomsInvoiceItem> items,
        string currency = "EUR")
    {
        var order = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId);

        string customerName = order?.CustomerName ?? "International Consignee";
        string rawPhone = order?.PhoneNumber ?? "";
        string address = order != null ? $"{order.ShippingAddress}, {order.DelegationOrCommune}, {order.GovernorateOrWilaya}" : "Consignee Address";

        string normalizedPhone = NormalizePhoneNumber(rawPhone, destinationCountry);
        decimal totalDeclaredValue = items.Sum(i => i.DeclaredTotalValue);

        var invoice = new CustomsCommercialInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            InvoiceNumber = $"EXP-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            ExporterName = "SGrowing International S.U.A.R.L.",
            ExporterCountry = "TN",
            DestinationCountry = destinationCountry.Trim().ToUpperInvariant(),
            ConsigneeCustomerName = customerName,
            ConsigneePhoneE164 = normalizedPhone,
            ConsigneeAddress = address,
            Currency = currency.Trim().ToUpperInvariant(),
            TotalDeclaredCustomsValue = totalDeclaredValue,
            TotalWeightKg = Math.Max(0.5m, items.Count * 0.4m),
            Incoterm = "DAP",
            ItemsJson = JsonSerializer.Serialize(items),
            ClearanceIssuedAtUtc = DateTime.UtcNow
        };

        _db.CustomsCommercialInvoices.Add(invoice);
        await _db.SaveChangesAsync();

        return invoice;
    }

    public async Task<List<CustomsCommercialInvoice>> ListCommercialInvoicesAsync(Guid tenantId)
    {
        return await _db.CustomsCommercialInvoices
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.ClearanceIssuedAtUtc)
            .ToListAsync();
    }
}
