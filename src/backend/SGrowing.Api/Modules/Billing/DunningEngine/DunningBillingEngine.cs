using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Billing.DunningEngine;

public interface IDunningBillingEngine
{
    TaxCalculationResult CalculateTax(decimal subtotal, string countryCode, string? vatNumber);
    BillingInvoice GenerateInvoice(CreateInvoiceRequest request);
    BillingInvoice? GetInvoice(string tenantId, string invoiceId);
    List<BillingInvoice> ListInvoices(string tenantId);
    DunningRecoverySummary ProcessDunningRetry(ProcessDunningRetryRequest request);
    List<DunningEvent> GetDunningAuditLog(string invoiceId);
}

public class DunningBillingEngine : IDunningBillingEngine
{
    private readonly ConcurrentDictionary<string, BillingInvoice> _invoices = new();
    private readonly ConcurrentDictionary<string, List<DunningEvent>> _dunningLogs = new();

    private static string BuildKey(string tenantId, string invoiceId) => $"{tenantId}::{invoiceId}";

    public TaxCalculationResult CalculateTax(decimal subtotal, string countryCode, string? vatNumber)
    {
        var code = countryCode.ToUpperInvariant().Trim();

        // 1. EU VAT Reverse Charge check
        var isEu = IsEuCountry(code);
        if (isEu && !string.IsNullOrWhiteSpace(vatNumber) && IsValidVatFormat(code, vatNumber))
        {
            return new TaxCalculationResult(
                Subtotal: subtotal,
                TaxRatePercent: 0.0m,
                TaxAmount: 0.0m,
                TotalAmount: subtotal,
                TaxJurisdiction: $"EU Cross-Border B2B ({code})",
                ExemptionReason: TaxExemptionReason.EuViesReverseCharge
            );
        }

        // 2. Standard rates per jurisdiction
        var (rate, jurisdiction) = code switch
        {
            "US" => (8.25m, "United States (State Economic Nexus Avg)"),
            "GB" => (20.0m, "United Kingdom HMRC VAT"),
            "DE" => (19.0m, "Germany MwSt"),
            "FR" => (20.0m, "France TVA"),
            "CA" => (13.0m, "Canada Harmonized Sales Tax (HST)"),
            "AU" => (10.0m, "Australia GST"),
            _ => (0.0m, "Global International (Tax Exemption)")
        };

        var taxAmount = Math.Round(subtotal * (rate / 100m), 2, MidpointRounding.AwayFromZero);
        var total = subtotal + taxAmount;

        return new TaxCalculationResult(
            Subtotal: subtotal,
            TaxRatePercent: rate,
            TaxAmount: taxAmount,
            TotalAmount: total,
            TaxJurisdiction: jurisdiction,
            ExemptionReason: rate == 0 ? TaxExemptionReason.None : TaxExemptionReason.None
        );
    }

    public BillingInvoice GenerateInvoice(CreateInvoiceRequest request)
    {
        var tax = CalculateTax(request.Subtotal, request.CountryCode, request.VatNumber);
        var invoiceId = "inv_" + Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        var rawData = $"{request.TenantId}:{invoiceId}:{request.CustomerId}:{tax.TotalAmount}:{request.Currency}:{now:O}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawData)));

        var invoice = new BillingInvoice(
            InvoiceId: invoiceId,
            TenantId: request.TenantId,
            CustomerId: request.CustomerId,
            Subtotal: tax.Subtotal,
            TaxAmount: tax.TaxAmount,
            Total: tax.TotalAmount,
            Currency: request.Currency.ToUpperInvariant(),
            Status: InvoiceStatus.PastDue,
            DunningAttempts: 0,
            DueDateUtc: now.AddDays(7),
            PaidAtUtc: null,
            DigitalSealHash: hash
        );

        _invoices[BuildKey(request.TenantId, invoiceId)] = invoice;
        return invoice;
    }

    public BillingInvoice? GetInvoice(string tenantId, string invoiceId)
    {
        _invoices.TryGetValue(BuildKey(tenantId, invoiceId), out var inv);
        return inv;
    }

    public List<BillingInvoice> ListInvoices(string tenantId)
    {
        return _invoices.Values
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.DueDateUtc)
            .ToList();
    }

    public DunningRecoverySummary ProcessDunningRetry(ProcessDunningRetryRequest request)
    {
        var key = BuildKey(request.TenantId, request.InvoiceId);
        if (!_invoices.TryGetValue(key, out var invoice))
        {
            throw new KeyNotFoundException($"Invoice '{request.InvoiceId}' not found.");
        }

        var newAttempts = invoice.DunningAttempts + 1;
        var now = DateTime.UtcNow;
        var log = _dunningLogs.GetOrAdd(invoice.InvoiceId, _ => new List<DunningEvent>());

        if (request.SimulatePaymentSuccess)
        {
            var paidInvoice = invoice with
            {
                Status = InvoiceStatus.Paid,
                PaidAtUtc = now,
                DunningAttempts = newAttempts
            };
            _invoices[key] = paidInvoice;

            log.Add(new DunningEvent(
                EventId: Guid.NewGuid().ToString("N")[..8],
                InvoiceId: invoice.InvoiceId,
                Action: DunningActionType.RetryPayment,
                Succeeded: true,
                Details: $"Payment gateway recovered total {invoice.Total} {invoice.Currency} on attempt #{newAttempts}.",
                TimestampUtc: now
            ));

            return new DunningRecoverySummary(
                InvoiceId: invoice.InvoiceId,
                Status: InvoiceStatus.Paid,
                AttemptNumber: newAttempts,
                RecoveredAmount: invoice.Total,
                NextAction: "Account in good standing. No further dunning required."
            );
        }

        // Failure progression
        var (action, nextAction, newStatus) = newAttempts switch
        {
            1 => (DunningActionType.SendEmailWarning, "Scheduled Day 3 payment retry + grace period SMS", InvoiceStatus.PastDue),
            2 => (DunningActionType.SendSmsWarning, "Scheduled Day 7 final notice + automatic plan downgrade", InvoiceStatus.PastDue),
            _ => (DunningActionType.DowngradeTier, "Account downgraded to Free tier. Account suspended.", InvoiceStatus.Uncollectible)
        };

        var updatedInvoice = invoice with
        {
            Status = newStatus,
            DunningAttempts = newAttempts
        };
        _invoices[key] = updatedInvoice;

        log.Add(new DunningEvent(
            EventId: Guid.NewGuid().ToString("N")[..8],
            InvoiceId: invoice.InvoiceId,
            Action: action,
            Succeeded: false,
            Details: $"Attempt #{newAttempts} failed. Triggered {action}. {nextAction}",
            TimestampUtc: now
        ));

        return new DunningRecoverySummary(
            InvoiceId: invoice.InvoiceId,
            Status: newStatus,
            AttemptNumber: newAttempts,
            RecoveredAmount: 0m,
            NextAction: nextAction
        );
    }

    public List<DunningEvent> GetDunningAuditLog(string invoiceId)
    {
        return _dunningLogs.TryGetValue(invoiceId, out var log)
            ? new List<DunningEvent>(log)
            : new List<DunningEvent>();
    }

    private static bool IsEuCountry(string code)
    {
        string[] eu = ["AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK"];
        return eu.Contains(code);
    }

    private static bool IsValidVatFormat(string countryCode, string vat)
    {
        var clean = vat.Trim().ToUpperInvariant();
        return clean.StartsWith(countryCode) && clean.Length >= 8;
    }
}
