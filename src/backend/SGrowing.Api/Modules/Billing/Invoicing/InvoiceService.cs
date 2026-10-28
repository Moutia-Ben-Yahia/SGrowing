using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Billing.Invoicing;

public interface IInvoiceService
{
    Task<InvoiceRecord> CreateInvoiceAsync(Guid tenantId, CreateInvoiceRequest req, CancellationToken ct = default);
    Task<InvoiceRecord?> CreateInvoiceFromDealAsync(Guid tenantId, Guid dealId, string currency = "USD", decimal taxPercentage = 19.0m, CancellationToken ct = default);
    Task<IEnumerable<InvoiceRecord>> GetInvoicesAsync(Guid tenantId, InvoiceStatus? status = null, CancellationToken ct = default);
    Task<InvoiceRecord?> GetInvoiceByIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);
    Task<PaymentRecord?> RecordPaymentAsync(Guid tenantId, Guid invoiceId, RecordPaymentRequest req, CancellationToken ct = default);
    Task<CurrencyConversionResult> ConvertCurrencyAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken ct = default);
    Task<IEnumerable<CurrencyRateRecord>> GetCurrencyRatesAsync(CancellationToken ct = default);
}

public class InvoiceService : IInvoiceService
{
    private readonly SGrowingDbContext _db;

    private static readonly Dictionary<string, decimal> DefaultRatesToBaseUsd = new(StringComparer.OrdinalIgnoreCase)
    {
        { "USD", 1.00m },
        { "EUR", 0.92m },
        { "TND", 3.12m },
        { "MAD", 10.15m },
        { "EGP", 48.50m }
    };

    public InvoiceService(SGrowingDbContext db)
    {
        _db = db;
    }

    private async Task EnsureCurrencyRatesAsync(CancellationToken ct)
    {
        if (!await _db.CurrencyRates.AnyAsync(ct))
        {
            foreach (var kv in DefaultRatesToBaseUsd)
            {
                _db.CurrencyRates.Add(new CurrencyRateRecord
                {
                    CurrencyCode = kv.Key.ToUpperInvariant(),
                    RateToBaseUsd = kv.Value,
                    LastUpdatedUtc = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IEnumerable<CurrencyRateRecord>> GetCurrencyRatesAsync(CancellationToken ct = default)
    {
        await EnsureCurrencyRatesAsync(ct);
        return await _db.CurrencyRates.OrderBy(r => r.CurrencyCode).ToListAsync(ct);
    }

    private async Task<decimal> GetExchangeRateAsync(string currencyCode, CancellationToken ct)
    {
        await EnsureCurrencyRatesAsync(ct);
        var record = await _db.CurrencyRates.FirstOrDefaultAsync(r => r.CurrencyCode.ToUpper() == currencyCode.ToUpper(), ct);
        if (record != null) return record.RateToBaseUsd;
        if (DefaultRatesToBaseUsd.TryGetValue(currencyCode, out var defaultRate)) return defaultRate;
        return 1.00m;
    }

    public async Task<CurrencyConversionResult> ConvertCurrencyAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        var fromRate = await GetExchangeRateAsync(fromCurrency, ct);
        var toRate = await GetExchangeRateAsync(toCurrency, ct);

        // Convert from source to USD base, then to target
        var baseUsd = fromRate > 0 ? amount / fromRate : amount;
        var convertedAmount = Math.Round(baseUsd * toRate, 4);
        var effectiveRate = fromRate > 0 ? Math.Round(toRate / fromRate, 6) : toRate;

        return new CurrencyConversionResult(
            SourceAmount: amount,
            FromCurrency: fromCurrency.ToUpperInvariant(),
            TargetAmount: convertedAmount,
            ToCurrency: toCurrency.ToUpperInvariant(),
            ExchangeRate: effectiveRate
        );
    }

    public async Task<InvoiceRecord> CreateInvoiceAsync(Guid tenantId, CreateInvoiceRequest req, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(req.ClientName)) throw new ArgumentException("ClientName is required.", nameof(req));

        var currency = (req.Currency ?? "USD").ToUpperInvariant();
        var rate = await GetExchangeRateAsync(currency, ct);

        var invoice = new InvoiceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DealId = req.DealId,
            ContactId = req.ContactId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            ClientName = req.ClientName,
            ClientEmail = req.ClientEmail,
            Currency = currency,
            ExchangeRate = rate,
            TaxPercentage = req.TaxPercentage,
            Status = InvoiceStatus.Draft,
            DueDateUtc = req.DueDateUtc ?? DateTime.UtcNow.AddDays(30),
            Notes = req.Notes,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        decimal subtotal = 0m;
        if (req.LineItems != null && req.LineItems.Count > 0)
        {
            foreach (var item in req.LineItems)
            {
                var lineTotal = Math.Round(item.Quantity * item.UnitPrice, 2);
                subtotal += lineTotal;
                invoice.LineItems.Add(new InvoiceLineItemRecord
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = lineTotal
                });
            }
        }

        invoice.SubTotal = subtotal;
        invoice.TaxAmount = Math.Round(subtotal * (invoice.TaxPercentage / 100m), 2);
        invoice.TotalAmount = invoice.SubTotal + invoice.TaxAmount;
        invoice.TotalBaseAmountUsd = rate > 0 ? Math.Round(invoice.TotalAmount / rate, 2) : invoice.TotalAmount;

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task<InvoiceRecord?> CreateInvoiceFromDealAsync(Guid tenantId, Guid dealId, string currency = "USD", decimal taxPercentage = 19.0m, CancellationToken ct = default)
    {
        var deal = await _db.Deals.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == dealId, ct);
        if (deal == null) return null;

        var clientName = "Deal Customer";
        string? clientEmail = null;

        if (deal.ContactId != Guid.Empty)
        {
            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == deal.ContactId, ct);
            if (contact != null)
            {
                clientName = $"{contact.FirstName} {contact.LastName}".Trim();
                clientEmail = contact.Email;
            }
        }

        var normalizedCurrency = (currency ?? "USD").ToUpperInvariant();
        var rate = await GetExchangeRateAsync(normalizedCurrency, ct);

        // Convert base USD deal amount to selected currency
        var subtotal = Math.Round(deal.Amount * rate, 2);

        var req = new CreateInvoiceRequest(
            ClientName: string.IsNullOrWhiteSpace(clientName) ? deal.Title : clientName,
            ClientEmail: clientEmail,
            Currency: normalizedCurrency,
            TaxPercentage: taxPercentage,
            DealId: deal.Id,
            ContactId: deal.ContactId != Guid.Empty ? deal.ContactId : null,
            DueDateUtc: deal.ExpectedCloseDate?.AddDays(30) ?? DateTime.UtcNow.AddDays(30),
            Notes: $"Generated autonomously from CRM Deal: '{deal.Title}' (Stage: {deal.Stage}).",
            LineItems: new List<CreateInvoiceLineItemRequest>
            {
                new CreateInvoiceLineItemRequest(
                    Description: $"Contract Engagement - {deal.Title}",
                    Quantity: 1,
                    UnitPrice: subtotal
                )
            }
        );

        return await CreateInvoiceAsync(tenantId, req, ct);
    }

    public async Task<IEnumerable<InvoiceRecord>> GetInvoicesAsync(Guid tenantId, InvoiceStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query.OrderByDescending(i => i.CreatedAtUtc).ToListAsync(ct);
    }

    public async Task<InvoiceRecord?> GetInvoiceByIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        return await _db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, ct);
    }

    public async Task<PaymentRecord?> RecordPaymentAsync(Guid tenantId, Guid invoiceId, RecordPaymentRequest req, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, ct);
        if (invoice == null) return null;

        var payment = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            Amount = req.Amount,
            Currency = invoice.Currency,
            PaymentMethod = req.PaymentMethod,
            TransactionReference = req.TransactionReference ?? $"PAY-{Guid.NewGuid():N}"[..16].ToUpper(),
            PaidAtUtc = DateTime.UtcNow,
            Status = "Success"
        };

        _db.Payments.Add(payment);

        // Check total payments against invoice total
        var existingPaid = await _db.Payments
            .Where(p => p.InvoiceId == invoiceId && p.Status == "Success")
            .SumAsync(p => p.Amount, ct);

        if (existingPaid + req.Amount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAtUtc = DateTime.UtcNow;
        }
        else
        {
            invoice.Status = InvoiceStatus.Sent;
        }

        invoice.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return payment;
    }
}
