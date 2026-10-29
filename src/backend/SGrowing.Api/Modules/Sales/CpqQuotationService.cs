using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Sales;

public enum CpqQuoteStatus
{
    Draft = 1,
    PendingApproval = 2,
    SentToClient = 3,
    Accepted = 4,
    Rejected = 5,
    ConvertedToOrder = 6
}

public class VolumeTier
{
    public int MinQuantity { get; set; }
    public int? MaxQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }

    public VolumeTier() { }

    public VolumeTier(int minQuantity, int? maxQuantity, decimal unitPrice, decimal? discountPercentage = null)
    {
        MinQuantity = minQuantity;
        MaxQuantity = maxQuantity;
        UnitPrice = unitPrice;
        DiscountPercentage = discountPercentage;
    }
}

public class PriceBookEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PriceBookId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal CostPrice { get; set; }
    public List<VolumeTier> VolumeTiers { get; set; } = new();
}

public class PriceBook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public bool IsDefault { get; set; } = false;
    public List<PriceBookEntry> PriceBookEntries { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public class CpqQuoteLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal LineTotal { get; set; }
    public decimal LineMarginPercentage { get; set; }
}

public class CpqQuote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public Guid? DealId { get; set; }
    public CpqQuoteStatus Status { get; set; } = CpqQuoteStatus.Draft;
    public string Currency { get; set; } = "USD";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal OverallMarginPercentage { get; set; }
    public bool RequiresManagerApproval { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovalNotes { get; set; }
    public List<CpqQuoteLineItem> LineItems { get; set; } = new();
    public DateTime ExpirationDateUtc { get; set; } = DateTime.UtcNow.AddDays(30);
    public string TermsAndConditions { get; set; } = "Payment due within 30 days. Standard enterprise warranty applies.";
    public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid? ConvertedOrderId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

// Request and Response DTOs
public record CreatePriceBookRequest(
    string Name,
    string Currency,
    bool IsDefault,
    List<PriceBookEntryDto>? Entries = null
);

public record PriceBookEntryDto(
    string ProductSku,
    string ProductName,
    decimal BasePrice,
    decimal CostPrice,
    List<VolumeTierDto>? VolumeTiers = null
);

public record VolumeTierDto(
    int MinQuantity,
    int? MaxQuantity,
    decimal UnitPrice
);

public record CreateQuoteRequest(
    Guid? TenantId,
    Guid CustomerId,
    string CustomerName,
    string? CustomerEmail,
    Guid? DealId,
    string? Currency,
    string? TermsAndConditions,
    DateTime? ExpirationDateUtc,
    List<QuoteLineItemInput>? LineItems = null,
    Guid? PriceBookId = null
);

public record QuoteLineItemInput(
    string ProductSku,
    int Quantity,
    decimal? OverrideUnitPrice = null,
    decimal DiscountPercentage = 0m,
    string? ProductName = null
);

public record AddOrUpdateLinesRequest(
    List<QuoteLineItemInput> Lines,
    Guid? PriceBookId = null
);

public record ApproveQuoteRequest(
    string ManagerNameOrEmail,
    string? Notes = null
);

public record RejectQuoteRequest(
    string ManagerNameOrEmail,
    string Reason
);

public record ChecksumValidationResult(
    bool IsValid,
    string StoredChecksum,
    string ComputedChecksum,
    string Details
);

public record QuoteToOrderConversionResult(
    Guid QuoteId,
    string QuoteNumber,
    Guid OrderId,
    string OrderNumber,
    Guid? DealId,
    string DealStage,
    decimal TotalAmount,
    string Currency,
    int LineItemCount,
    DateTime ConvertedAtUtc
);

public record PdfPreviewDto(
    Guid QuoteId,
    string QuoteNumber,
    string CompanyName,
    string CompanyTaxId,
    string CustomerName,
    string CustomerEmail,
    string Currency,
    List<CpqQuoteLineItem> LineItems,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal OverallMarginPercentage,
    bool RequiresManagerApproval,
    string? ApprovedBy,
    DateTime ExpirationDateUtc,
    int DaysUntilExpiration,
    string TermsAndConditions,
    string ChecksumSha256,
    string FormattedHtmlPreview
);

public interface ICpqQuotationService
{
    Task<List<PriceBook>> GetPriceBooksAsync(Guid tenantId);
    Task<PriceBook> CreatePriceBookAsync(Guid tenantId, CreatePriceBookRequest request);
    Task<PriceBook?> GetPriceBookByIdAsync(Guid tenantId, Guid priceBookId);
    Task<List<CpqQuote>> GetQuotesAsync(Guid tenantId, CpqQuoteStatus? status = null, Guid? customerId = null);
    Task<CpqQuote> CreateQuoteAsync(Guid tenantId, CreateQuoteRequest request);
    Task<CpqQuote> GetQuoteByIdAsync(Guid tenantId, Guid quoteId);
    Task<CpqQuote> UpdateLinesAsync(Guid tenantId, Guid quoteId, List<QuoteLineItemInput> lines, Guid? priceBookId = null);
    Task<CpqQuote> RequestApprovalAsync(Guid tenantId, Guid quoteId, string? notes = null);
    Task<CpqQuote> ApproveQuoteAsync(Guid tenantId, Guid quoteId, string managerNameOrEmail, string? notes = null);
    Task<CpqQuote> RejectQuoteAsync(Guid tenantId, Guid quoteId, string managerNameOrEmail, string reason);
    Task<CpqQuote> SendQuoteAsync(Guid tenantId, Guid quoteId);
    Task<QuoteToOrderConversionResult> AcceptQuoteAsync(Guid tenantId, Guid quoteId);
    Task<PdfPreviewDto> GetPdfPreviewAsync(Guid tenantId, Guid quoteId);
    ChecksumValidationResult ValidateChecksum(CpqQuote quote);
}

public class CpqQuotationService : ICpqQuotationService
{
    public const decimal DefaultMarginPolicyThreshold = 25.0m; // Aggregate margin < 25% requires manager approval
    public const decimal DefaultMaxDiscountPolicyThreshold = 20.0m; // Discount > 20% requires manager approval

    private readonly SGrowingDbContext _db;
    private static readonly List<PriceBook> _inMemoryPriceBooks = new();
    private static readonly List<CpqQuote> _inMemoryQuotes = new();
    private static readonly object _syncLock = new();

    public CpqQuotationService(SGrowingDbContext db)
    {
        _db = db;
        EnsureDefaultPriceBookSeeded();
    }

    private void EnsureDefaultPriceBookSeeded()
    {
        lock (_syncLock)
        {
            if (_inMemoryPriceBooks.Count > 0) return;

            var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            var enterprisePriceBook = new PriceBook
            {
                Id = Guid.Parse("aaaa1111-bbbb-2222-cccc-333344445555"),
                TenantId = defaultTenantId,
                Name = "Standard Enterprise 2026 Price Book",
                Currency = "USD",
                IsDefault = true,
                CreatedAtUtc = DateTime.UtcNow,
                PriceBookEntries = new List<PriceBookEntry>
                {
                    new PriceBookEntry
                    {
                        Id = Guid.NewGuid(),
                        ProductSku = "ENTERPRISE-CRM-LIC",
                        ProductName = "Enterprise Growth CRM Seat",
                        BasePrice = 120.0m,
                        CostPrice = 30.0m,
                        VolumeTiers = new List<VolumeTier>
                        {
                            new(1, 9, 120.0m),
                            new(10, 49, 100.0m),
                            new(50, 99, 85.0m),
                            new(100, null, 70.0m)
                        }
                    },
                    new PriceBookEntry
                    {
                        Id = Guid.NewGuid(),
                        ProductSku = "AUTONOMOUS-AI-SEAT",
                        ProductName = "Autonomous AI Agent Fleet Seat",
                        BasePrice = 250.0m,
                        CostPrice = 60.0m,
                        VolumeTiers = new List<VolumeTier>
                        {
                            new(1, 4, 250.0m),
                            new(5, 19, 210.0m),
                            new(20, null, 175.0m)
                        }
                    },
                    new PriceBookEntry
                    {
                        Id = Guid.NewGuid(),
                        ProductSku = "DEDICATED-IP-POOL",
                        ProductName = "Dedicated Outbound IP Pool & Warmup",
                        BasePrice = 500.0m,
                        CostPrice = 150.0m,
                        VolumeTiers = new List<VolumeTier>
                        {
                            new(1, 2, 500.0m),
                            new(3, 9, 420.0m),
                            new(10, null, 350.0m)
                        }
                    },
                    new PriceBookEntry
                    {
                        Id = Guid.NewGuid(),
                        ProductSku = "CUSTOM-INTEGRATION-HR",
                        ProductName = "Custom ERP/WMS Integration Hours",
                        BasePrice = 150.0m,
                        CostPrice = 120.0m, // Low base margin 20% to exercise margin guard
                        VolumeTiers = new List<VolumeTier>
                        {
                            new(1, 19, 150.0m),
                            new(20, null, 140.0m)
                        }
                    }
                }
            };

            _inMemoryPriceBooks.Add(enterprisePriceBook);
        }
    }

    public Task<List<PriceBook>> GetPriceBooksAsync(Guid tenantId)
    {
        lock (_syncLock)
        {
            var books = _inMemoryPriceBooks
                .Where(p => p.TenantId == tenantId || p.TenantId == Guid.Parse("11111111-1111-1111-1111-111111111111"))
                .ToList();
            return Task.FromResult(books);
        }
    }

    public Task<PriceBook?> GetPriceBookByIdAsync(Guid tenantId, Guid priceBookId)
    {
        lock (_syncLock)
        {
            var book = _inMemoryPriceBooks.FirstOrDefault(p => p.Id == priceBookId && 
                (p.TenantId == tenantId || p.TenantId == Guid.Parse("11111111-1111-1111-1111-111111111111")));
            return Task.FromResult(book);
        }
    }

    public Task<PriceBook> CreatePriceBookAsync(Guid tenantId, CreatePriceBookRequest request)
    {
        lock (_syncLock)
        {
            if (request.IsDefault)
            {
                foreach (var b in _inMemoryPriceBooks.Where(b => b.TenantId == tenantId))
                {
                    b.IsDefault = false;
                }
            }

            var priceBookId = Guid.NewGuid();
            var priceBook = new PriceBook
            {
                Id = priceBookId,
                TenantId = tenantId,
                Name = request.Name,
                Currency = request.Currency ?? "USD",
                IsDefault = request.IsDefault,
                CreatedAtUtc = DateTime.UtcNow,
                PriceBookEntries = request.Entries?.Select(e => new PriceBookEntry
                {
                    Id = Guid.NewGuid(),
                    PriceBookId = priceBookId,
                    ProductSku = e.ProductSku,
                    ProductName = e.ProductName,
                    BasePrice = e.BasePrice,
                    CostPrice = e.CostPrice,
                    VolumeTiers = e.VolumeTiers?.Select(v => new VolumeTier(v.MinQuantity, v.MaxQuantity, v.UnitPrice)).ToList() ?? new()
                }).ToList() ?? new()
            };

            _inMemoryPriceBooks.Add(priceBook);
            return Task.FromResult(priceBook);
        }
    }

    public Task<List<CpqQuote>> GetQuotesAsync(Guid tenantId, CpqQuoteStatus? status = null, Guid? customerId = null)
    {
        lock (_syncLock)
        {
            var query = _inMemoryQuotes.Where(q => q.TenantId == tenantId);
            if (status.HasValue)
            {
                query = query.Where(q => q.Status == status.Value);
            }
            if (customerId.HasValue)
            {
                query = query.Where(q => q.CustomerId == customerId.Value);
            }

            return Task.FromResult(query.OrderByDescending(q => q.CreatedAtUtc).ToList());
        }
    }

    public Task<CpqQuote> GetQuoteByIdAsync(Guid tenantId, Guid quoteId)
    {
        lock (_syncLock)
        {
            var quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId);
            if (quote == null)
            {
                throw new KeyNotFoundException($"CPQ Quote with Id '{quoteId}' not found for tenant '{tenantId}'.");
            }
            return Task.FromResult(quote);
        }
    }

    public async Task<CpqQuote> CreateQuoteAsync(Guid tenantId, CreateQuoteRequest request)
    {
        var quoteId = Guid.NewGuid();
        var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomSuffix = Random.Shared.Next(1000, 9999);
        var quoteNumber = $"CPQ-{dateStamp}-{randomSuffix}";

        var quote = new CpqQuote
        {
            Id = quoteId,
            TenantId = tenantId,
            QuoteNumber = quoteNumber,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            DealId = request.DealId,
            Currency = request.Currency ?? "USD",
            Status = CpqQuoteStatus.Draft,
            ExpirationDateUtc = request.ExpirationDateUtc ?? DateTime.UtcNow.AddDays(30),
            TermsAndConditions = request.TermsAndConditions ?? "Payment due within 30 days. Standard enterprise SLA applies.",
            CreatedAtUtc = DateTime.UtcNow
        };

        if (request.LineItems != null && request.LineItems.Count > 0)
        {
            var priceBook = await ResolvePriceBookAsync(tenantId, request.PriceBookId);
            ApplyLinesAndRecalculate(quote, request.LineItems, priceBook);
        }
        else
        {
            quote.ChecksumSha256 = ComputeSha256Checksum(quote);
        }

        lock (_syncLock)
        {
            _inMemoryQuotes.Add(quote);
        }

        return quote;
    }

    public async Task<CpqQuote> UpdateLinesAsync(Guid tenantId, Guid quoteId, List<QuoteLineItemInput> lines, Guid? priceBookId = null)
    {
        CpqQuote quote;
        lock (_syncLock)
        {
            quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId)
                ?? throw new KeyNotFoundException($"Quote with Id '{quoteId}' not found.");
        }

        if (quote.Status is CpqQuoteStatus.Accepted or CpqQuoteStatus.ConvertedToOrder)
        {
            throw new InvalidOperationException($"Cannot update lines for a quote in status '{quote.Status}'.");
        }

        var priceBook = await ResolvePriceBookAsync(tenantId, priceBookId);
        ApplyLinesAndRecalculate(quote, lines, priceBook);
        quote.UpdatedAtUtc = DateTime.UtcNow;

        return quote;
    }

    public Task<CpqQuote> RequestApprovalAsync(Guid tenantId, Guid quoteId, string? notes = null)
    {
        lock (_syncLock)
        {
            var quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId)
                ?? throw new KeyNotFoundException($"Quote with Id '{quoteId}' not found.");

            if (!quote.RequiresManagerApproval)
            {
                throw new InvalidOperationException("Quote meets margin and discount policy thresholds and does not require manager approval.");
            }

            quote.Status = CpqQuoteStatus.PendingApproval;
            quote.ApprovalNotes = notes;
            quote.UpdatedAtUtc = DateTime.UtcNow;
            quote.ChecksumSha256 = ComputeSha256Checksum(quote);

            return Task.FromResult(quote);
        }
    }

    public Task<CpqQuote> ApproveQuoteAsync(Guid tenantId, Guid quoteId, string managerNameOrEmail, string? notes = null)
    {
        lock (_syncLock)
        {
            var quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId)
                ?? throw new KeyNotFoundException($"Quote with Id '{quoteId}' not found.");

            quote.ApprovedBy = managerNameOrEmail;
            quote.ApprovedAtUtc = DateTime.UtcNow;
            quote.RequiresManagerApproval = false; // Manager sign-off unblocks dispatch
            quote.ApprovalNotes = notes ?? quote.ApprovalNotes;
            if (quote.Status == CpqQuoteStatus.PendingApproval)
            {
                quote.Status = CpqQuoteStatus.Draft; // Ready for sending to client
            }
            quote.UpdatedAtUtc = DateTime.UtcNow;
            quote.ChecksumSha256 = ComputeSha256Checksum(quote);

            return Task.FromResult(quote);
        }
    }

    public Task<CpqQuote> RejectQuoteAsync(Guid tenantId, Guid quoteId, string managerNameOrEmail, string reason)
    {
        lock (_syncLock)
        {
            var quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId)
                ?? throw new KeyNotFoundException($"Quote with Id '{quoteId}' not found.");

            quote.ApprovedBy = managerNameOrEmail;
            quote.ApprovedAtUtc = DateTime.UtcNow;
            quote.ApprovalNotes = $"Rejected: {reason}";
            quote.Status = CpqQuoteStatus.Rejected;
            quote.UpdatedAtUtc = DateTime.UtcNow;
            quote.ChecksumSha256 = ComputeSha256Checksum(quote);

            return Task.FromResult(quote);
        }
    }

    public Task<CpqQuote> SendQuoteAsync(Guid tenantId, Guid quoteId)
    {
        lock (_syncLock)
        {
            var quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId)
                ?? throw new KeyNotFoundException($"Quote with Id '{quoteId}' not found.");

            if (quote.RequiresManagerApproval && string.IsNullOrEmpty(quote.ApprovedBy))
            {
                throw new InvalidOperationException(
                    $"Quote '{quote.QuoteNumber}' requires manager approval before dispatching to client. " +
                    $"Overall margin is {quote.OverallMarginPercentage:F1}% (minimum policy {DefaultMarginPolicyThreshold}%) or discounts exceed policy.");
            }

            if (quote.Status is CpqQuoteStatus.Accepted or CpqQuoteStatus.ConvertedToOrder)
            {
                throw new InvalidOperationException($"Cannot send quote '{quote.QuoteNumber}' as it is already {quote.Status}.");
            }

            quote.Status = CpqQuoteStatus.SentToClient;
            quote.UpdatedAtUtc = DateTime.UtcNow;
            quote.ChecksumSha256 = ComputeSha256Checksum(quote);

            return Task.FromResult(quote);
        }
    }

    public async Task<QuoteToOrderConversionResult> AcceptQuoteAsync(Guid tenantId, Guid quoteId)
    {
        CpqQuote quote;
        lock (_syncLock)
        {
            quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId)
                ?? throw new KeyNotFoundException($"Quote with Id '{quoteId}' not found.");
        }

        if (quote.ExpirationDateUtc < DateTime.UtcNow)
        {
            throw new InvalidOperationException($"Quote '{quote.QuoteNumber}' has expired on {quote.ExpirationDateUtc:yyyy-MM-dd}.");
        }

        if (quote.RequiresManagerApproval && string.IsNullOrEmpty(quote.ApprovedBy))
        {
            throw new InvalidOperationException($"Cannot accept quote '{quote.QuoteNumber}' because it requires manager approval.");
        }

        var orderId = Guid.NewGuid();
        var orderNumber = $"ORD-{quote.QuoteNumber}";

        // Persist confirmed order directly into DbContext
        var codOrder = new CodOrder
        {
            Id = orderId,
            TenantId = tenantId,
            OrderNumber = orderNumber,
            CustomerName = quote.CustomerName,
            PhoneNumber = "+1-555-CPQ-CONV",
            ShippingAddress = "Commercial CPQ Destination",
            GovernorateOrWilaya = "Headquarters",
            DelegationOrCommune = "B2B Sales",
            CountryCode = "US",
            TotalAmount = quote.GrandTotal,
            ShippingFee = 0m,
            CodCurrency = quote.Currency,
            Status = CodOrderStatus.Confirmed,
            ConfirmedAtUtc = DateTime.UtcNow,
            ConfirmedVia = "CPQ-DigitalAcceptance",
            Notes = $"Directly converted from CPQ Quote {quote.QuoteNumber}. Checksum: {quote.ChecksumSha256}",
            ItemsJson = JsonSerializer.Serialize(quote.LineItems.Select(l => new
            {
                l.ProductSku,
                l.ProductName,
                l.Quantity,
                l.UnitPrice,
                l.LineTotal
            }))
        };

        _db.CodOrders.Add(codOrder);

        // Update Deal to Won if linked
        string dealStageName = "N/A";
        if (quote.DealId.HasValue)
        {
            var deal = await _db.Deals.FirstOrDefaultAsync(d => d.Id == quote.DealId.Value && d.TenantId == tenantId);
            if (deal != null)
            {
                deal.Stage = DealStage.Won;
                deal.Amount = quote.GrandTotal;
                dealStageName = DealStage.Won.ToString();
            }
        }

        await _db.SaveChangesAsync();

        lock (_syncLock)
        {
            quote.Status = CpqQuoteStatus.ConvertedToOrder;
            quote.ConvertedOrderId = orderId;
            quote.UpdatedAtUtc = DateTime.UtcNow;
            quote.ChecksumSha256 = ComputeSha256Checksum(quote);
        }

        return new QuoteToOrderConversionResult(
            quote.Id,
            quote.QuoteNumber,
            orderId,
            orderNumber,
            quote.DealId,
            dealStageName,
            quote.GrandTotal,
            quote.Currency,
            quote.LineItems.Count,
            DateTime.UtcNow
        );
    }

    public Task<PdfPreviewDto> GetPdfPreviewAsync(Guid tenantId, Guid quoteId)
    {
        CpqQuote quote;
        lock (_syncLock)
        {
            quote = _inMemoryQuotes.FirstOrDefault(q => q.Id == quoteId && q.TenantId == tenantId)
                ?? throw new KeyNotFoundException($"Quote with Id '{quoteId}' not found.");
        }

        var daysUntilExpiration = Math.Max(0, (int)Math.Ceiling((quote.ExpirationDateUtc - DateTime.UtcNow).TotalDays));

        var linesTable = new StringBuilder();
        foreach (var l in quote.LineItems)
        {
            linesTable.Append($@"
              <tr style=""border-bottom: 1px solid #1e293b;"">
                <td style=""padding: 10px; font-family: monospace;"">{l.ProductSku}</td>
                <td style=""padding: 10px;"">{l.ProductName}</td>
                <td style=""padding: 10px; text-align: center;"">{l.Quantity}</td>
                <td style=""padding: 10px; text-align: right;"">{l.UnitPrice:F2} {quote.Currency}</td>
                <td style=""padding: 10px; text-align: right;"">{l.DiscountPercentage:F1}%</td>
                <td style=""padding: 10px; text-align: right; font-weight: 600;"">{l.LineTotal:F2} {quote.Currency}</td>
              </tr>");
        }

        var html = $@"
        <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0f172a; color: #f8fafc; padding: 32px; border-radius: 12px; max-width: 800px; margin: 0 auto; border: 1px solid #334155;"" dir=""ltr"">
          <div style=""display: flex; justify-content: space-between; border-bottom: 2px solid #3b82f6; padding-bottom: 16px; margin-bottom: 24px;"">
            <div>
              <h1 style=""margin: 0; color: #60a5fa; font-size: 26px;"">SGrowing Enterprise CPQ</h1>
              <p style=""margin: 4px 0 0; color: #94a3b8; font-size: 13px;"">Configure, Price & Quote Quotation Engine</p>
            </div>
            <div style=""text-align: right;"">
              <span style=""background: #1e293b; color: #38bdf8; padding: 4px 10px; border-radius: 6px; font-weight: bold; font-family: monospace;"">{quote.QuoteNumber}</span>
              <p style=""margin: 6px 0 0; color: #cbd5e1; font-size: 12px;"">Date: {quote.CreatedAtUtc:yyyy-MM-dd}</p>
            </div>
          </div>

          <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 24px;"">
            <div style=""background: #1e293b; padding: 14px; border-radius: 8px;"">
              <strong style=""color: #94a3b8; font-size: 12px; text-transform: uppercase;"">Client Details</strong>
              <h3 style=""margin: 6px 0 2px; color: #f1f5f9;"">{quote.CustomerName}</h3>
              <p style=""margin: 0; color: #94a3b8; font-size: 13px;"">{quote.CustomerEmail ?? "client@enterprise.internal"}</p>
            </div>
            <div style=""background: #1e293b; padding: 14px; border-radius: 8px;"">
              <strong style=""color: #94a3b8; font-size: 12px; text-transform: uppercase;"">Validity & Status</strong>
              <p style=""margin: 6px 0 2px; color: #f1f5f9; font-weight: 600;"">Expires: {quote.ExpirationDateUtc:yyyy-MM-dd} ({daysUntilExpiration} days remaining)</p>
              <p style=""margin: 0; color: #10b981; font-size: 13px;"">Status: {quote.Status}</p>
            </div>
          </div>

          <table style=""width: 100%; border-collapse: collapse; margin-bottom: 24px; font-size: 13px;"">
            <thead>
              <tr style=""background: #1e293b; color: #94a3b8; text-align: left;"">
                <th style=""padding: 10px;"">SKU</th>
                <th style=""padding: 10px;"">Description</th>
                <th style=""padding: 10px; text-align: center;"">Qty</th>
                <th style=""padding: 10px; text-align: right;"">Unit Price</th>
                <th style=""padding: 10px; text-align: right;"">Disc%</th>
                <th style=""padding: 10px; text-align: right;"">Total</th>
              </tr>
            </thead>
            <tbody>
              {linesTable}
            </tbody>
          </table>

          <div style=""display: flex; justify-content: flex-end; margin-bottom: 24px;"">
            <div style=""width: 260px; background: #1e293b; padding: 14px; border-radius: 8px; font-size: 14px;"">
              <div style=""display: flex; justify-content: space-between; margin-bottom: 6px; color: #94a3b8;"">
                <span>Subtotal:</span>
                <span>{quote.Subtotal:F2} {quote.Currency}</span>
              </div>
              <div style=""display: flex; justify-content: space-between; margin-bottom: 6px; color: #f43f5e;"">
                <span>Discount:</span>
                <span>-{quote.DiscountTotal:F2} {quote.Currency}</span>
              </div>
              <div style=""display: flex; justify-content: space-between; margin-bottom: 8px; color: #94a3b8;"">
                <span>Tax (0%):</span>
                <span>{quote.TaxTotal:F2} {quote.Currency}</span>
              </div>
              <div style=""border-top: 1px solid #334155; padding-top: 8px; display: flex; justify-content: space-between; font-weight: bold; font-size: 16px; color: #38bdf8;"">
                <span>Grand Total:</span>
                <span>{quote.GrandTotal:F2} {quote.Currency}</span>
              </div>
            </div>
          </div>

          <div style=""background: #090d16; border-left: 4px solid #3b82f6; padding: 12px; margin-bottom: 20px; border-radius: 4px; font-size: 12px;"">
            <strong style=""color: #cbd5e1;"">Terms & Conditions:</strong>
            <p style=""margin: 4px 0 0; color: #94a3b8;"">{quote.TermsAndConditions}</p>
          </div>

          <div style=""display: flex; justify-content: space-between; align-items: center; border-top: 1px solid #334155; padding-top: 14px; font-size: 11px; color: #64748b;"">
            <div>
              <span>Tamper-Evident SHA-256 Checksum: </span>
              <span style=""font-family: monospace; color: #10b981;"">{quote.ChecksumSha256}</span>
            </div>
            <div>
              <span>Approved By: {quote.ApprovedBy ?? "System Auto-Guard"}</span>
            </div>
          </div>
        </div>";

        var preview = new PdfPreviewDto(
            quote.Id,
            quote.QuoteNumber,
            "SGrowing Technologies Inc.",
            "US-TAX-8839210-CPQ",
            quote.CustomerName,
            quote.CustomerEmail ?? "client@enterprise.internal",
            quote.Currency,
            quote.LineItems,
            quote.Subtotal,
            quote.DiscountTotal,
            quote.TaxTotal,
            quote.GrandTotal,
            quote.OverallMarginPercentage,
            quote.RequiresManagerApproval,
            quote.ApprovedBy,
            quote.ExpirationDateUtc,
            daysUntilExpiration,
            quote.TermsAndConditions,
            quote.ChecksumSha256,
            html
        );

        return Task.FromResult(preview);
    }

    public ChecksumValidationResult ValidateChecksum(CpqQuote quote)
    {
        var expected = ComputeSha256Checksum(quote);
        var isValid = string.Equals(quote.ChecksumSha256, expected, StringComparison.OrdinalIgnoreCase);

        return new ChecksumValidationResult(
            isValid,
            quote.ChecksumSha256,
            expected,
            isValid ? "Cryptographic checksum is valid and verified against tamper detection." : "Checksum mismatch detected! One or more terms, prices, or line items have been altered."
        );
    }

    private async Task<PriceBook> ResolvePriceBookAsync(Guid tenantId, Guid? priceBookId)
    {
        if (priceBookId.HasValue)
        {
            var book = await GetPriceBookByIdAsync(tenantId, priceBookId.Value);
            if (book != null) return book;
        }

        var defaultBook = _inMemoryPriceBooks.FirstOrDefault(p => (p.TenantId == tenantId || p.TenantId == Guid.Parse("11111111-1111-1111-1111-111111111111")) && p.IsDefault)
            ?? _inMemoryPriceBooks.FirstOrDefault();

        if (defaultBook == null)
        {
            EnsureDefaultPriceBookSeeded();
            defaultBook = _inMemoryPriceBooks.First();
        }

        return defaultBook;
    }

    private void ApplyLinesAndRecalculate(CpqQuote quote, List<QuoteLineItemInput> inputLines, PriceBook priceBook)
    {
        var evaluatedLines = new List<CpqQuoteLineItem>();

        foreach (var input in inputLines)
        {
            var entry = priceBook.PriceBookEntries.FirstOrDefault(e => 
                string.Equals(e.ProductSku, input.ProductSku, StringComparison.OrdinalIgnoreCase));

            decimal unitPrice;
            decimal unitCost;
            string productName;

            if (entry != null)
            {
                productName = !string.IsNullOrWhiteSpace(input.ProductName) ? input.ProductName : entry.ProductName;
                unitCost = entry.CostPrice;

                if (input.OverrideUnitPrice.HasValue && input.OverrideUnitPrice.Value > 0)
                {
                    unitPrice = input.OverrideUnitPrice.Value;
                }
                else
                {
                    // Volume Tier Evaluation:
                    // Automatically calculates discounted unit price when line item quantity meets volume tier thresholds
                    var matchedTier = entry.VolumeTiers
                        .Where(vt => input.Quantity >= vt.MinQuantity && (!vt.MaxQuantity.HasValue || input.Quantity <= vt.MaxQuantity.Value))
                        .OrderByDescending(vt => vt.MinQuantity)
                        .FirstOrDefault();

                    unitPrice = matchedTier?.UnitPrice ?? entry.BasePrice;
                }
            }
            else
            {
                // Fallback for custom or unlisted SKU
                productName = input.ProductName ?? input.ProductSku;
                unitPrice = input.OverrideUnitPrice ?? 100.0m;
                unitCost = Math.Round(unitPrice * 0.70m, 2); // default 30% margin assumption
            }

            // Line calculations:
            // Line Gross = Quantity * UnitPrice
            // Discount amount = Line Gross * (DiscountPercentage / 100)
            // Line Total (Net Revenue) = Line Gross - Discount
            var lineGross = input.Quantity * unitPrice;
            var discountAmount = lineGross * (input.DiscountPercentage / 100.0m);
            var lineTotal = Math.Round(lineGross - discountAmount, 2);

            // Gross Margin Protection Guard:
            // Line item margin: Margin% = (NetRevenue - TotalCOGS) / NetRevenue * 100
            var lineCogs = input.Quantity * unitCost;
            var lineMarginPct = lineTotal > 0
                ? Math.Round(((lineTotal - lineCogs) / lineTotal) * 100.0m, 2)
                : 0m;

            evaluatedLines.Add(new CpqQuoteLineItem
            {
                Id = Guid.NewGuid(),
                ProductSku = input.ProductSku,
                ProductName = productName,
                UnitPrice = unitPrice,
                UnitCost = unitCost,
                Quantity = input.Quantity,
                DiscountPercentage = input.DiscountPercentage,
                LineTotal = lineTotal,
                LineMarginPercentage = lineMarginPct
            });
        }

        quote.LineItems = evaluatedLines;

        // Aggregate calculations:
        var totalGross = evaluatedLines.Sum(l => l.Quantity * l.UnitPrice);
        var totalNetRevenue = evaluatedLines.Sum(l => l.LineTotal);
        var totalDiscount = totalGross - totalNetRevenue;
        var totalCogs = evaluatedLines.Sum(l => l.Quantity * l.UnitCost);

        // Margin% = (NetRevenue - TotalCOGS) / NetRevenue * 100
        var aggregateMarginPct = totalNetRevenue > 0
            ? Math.Round(((totalNetRevenue - totalCogs) / totalNetRevenue) * 100.0m, 2)
            : 0m;

        quote.Subtotal = Math.Round(totalGross, 2);
        quote.DiscountTotal = Math.Round(totalDiscount, 2);
        quote.TaxTotal = 0m; // Default 0 tax for B2B exempt, configurable
        quote.GrandTotal = Math.Round(totalNetRevenue + quote.TaxTotal, 2);
        quote.OverallMarginPercentage = aggregateMarginPct;

        // Gross Margin Protection Guard:
        // If aggregate margin falls below policy threshold (default 25%) or discount exceeds maximum (default 20%),
        // sets RequiresManagerApproval = true and blocks sending quote to client until manager approves.
        var marginBelowThreshold = quote.OverallMarginPercentage < DefaultMarginPolicyThreshold;
        var discountExceedsLimit = evaluatedLines.Any(l => l.DiscountPercentage > DefaultMaxDiscountPolicyThreshold) ||
                                  (quote.Subtotal > 0 && ((quote.DiscountTotal / quote.Subtotal) * 100.0m) > DefaultMaxDiscountPolicyThreshold);

        if (marginBelowThreshold || discountExceedsLimit)
        {
            // If already approved and pricing hasn't dropped further, we can re-evaluate or flag
            if (string.IsNullOrEmpty(quote.ApprovedBy))
            {
                quote.RequiresManagerApproval = true;
            }
        }
        else
        {
            quote.RequiresManagerApproval = false;
        }

        quote.ChecksumSha256 = ComputeSha256Checksum(quote);
    }

    public static string ComputeSha256Checksum(CpqQuote quote)
    {
        var sb = new StringBuilder();
        sb.Append($"{quote.QuoteNumber}|{quote.TenantId}|{quote.CustomerId}|{quote.Currency}|");
        sb.Append($"{quote.Subtotal:F2}|{quote.DiscountTotal:F2}|{quote.TaxTotal:F2}|{quote.GrandTotal:F2}|{quote.OverallMarginPercentage:F2}|");
        sb.Append($"{quote.ExpirationDateUtc:yyyy-MM-ddTHH:mm:ssZ}|{quote.RequiresManagerApproval}|{quote.ApprovedBy ?? "UNAPPROVED"}|");
        sb.Append($"{quote.TermsAndConditions}|");
        foreach (var l in quote.LineItems.OrderBy(x => x.ProductSku))
        {
            sb.Append($"[{l.ProductSku}:{l.Quantity}:{l.UnitPrice:F2}:{l.UnitCost:F2}:{l.DiscountPercentage:F2}:{l.LineTotal:F2}]");
        }

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
