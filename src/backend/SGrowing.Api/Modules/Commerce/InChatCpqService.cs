using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Commerce;

public class CreateQuoteLineItemRequest
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class InChatCpqService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<InChatCpqService>? _logger;

    public InChatCpqService(SGrowingDbContext db, ILogger<InChatCpqService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<QuoteProposal> CreateQuoteAsync(
        Guid tenantId,
        Guid contactId,
        List<CreateQuoteLineItemRequest> items,
        Guid? companyId = null,
        Guid? dealId = null,
        decimal discountPercent = 0,
        decimal taxPercent = 0,
        string currency = "USD",
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var quoteCount = await _db.QuoteProposals.CountAsync(q => q.TenantId == tenantId, cancellationToken);
        var quoteNumber = $"QT-{DateTime.UtcNow:yyyyMM}-{(quoteCount + 1):D4}";

        var quote = QuoteProposal.Create(
            tenantId,
            contactId,
            quoteNumber,
            currency,
            companyId,
            dealId,
            discountPercent,
            taxPercent,
            notes);

        foreach (var item in items)
        {
            quote.AddLineItem(item.ProductId, item.Description, item.Quantity, item.UnitPrice, item.VariantId);
        }

        await _db.QuoteProposals.AddAsync(quote, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created Quote {QuoteNumber} ({TotalAmount:C}) for Contact {ContactId}",
            quote.QuoteNumber, quote.TotalAmount, contactId);

        return quote;
    }

    public async Task<string> GenerateInChatQuoteMessageAsync(
        Guid tenantId,
        Guid quoteId,
        string baseUrl = "https://app.sgrowing.com",
        CancellationToken cancellationToken = default)
    {
        var quote = await _db.QuoteProposals
            .Include(q => q.LineItems)
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quoteId, cancellationToken);

        if (quote == null)
        {
            throw new KeyNotFoundException($"Quote {quoteId} not found for tenant {tenantId}.");
        }

        quote.MarkSent();
        await _db.SaveChangesAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"📄 *Formal Proposal: {quote.QuoteNumber}*");
        sb.AppendLine("─────────────────────────");
        foreach (var item in quote.LineItems)
        {
            sb.AppendLine($"• {item.Description} (x{item.Quantity}) — {item.LineTotal:C2}");
        }
        sb.AppendLine("─────────────────────────");
        if (quote.DiscountPercent > 0)
        {
            sb.AppendLine($"Discount ({quote.DiscountPercent}%): -{(quote.Subtotal * (quote.DiscountPercent / 100m)):C2}");
        }
        sb.AppendLine($"*Total Amount:* {quote.TotalAmount:C2} {quote.Currency}");
        sb.AppendLine();
        sb.AppendLine($"👉 Accept proposal & checkout: {baseUrl.TrimEnd('/')}/quotes/{quote.Id}");

        return sb.ToString();
    }

    public async Task<QuoteProposal> AcceptQuoteAsync(
        Guid tenantId,
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var quote = await _db.QuoteProposals
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quoteId, cancellationToken);

        if (quote == null)
        {
            throw new KeyNotFoundException($"Quote {quoteId} not found.");
        }

        quote.MarkAccepted();

        if (quote.DealId.HasValue)
        {
            var deal = await _db.Deals.FindAsync(new object[] { quote.DealId.Value }, cancellationToken);
            if (deal != null)
            {
                deal.Stage = DealStage.Won;
            }
        }

        var outboxMsg = OutboxMessage.Create(
            tenantId,
            "QuoteAcceptedEvent",
            $"{{\"quoteId\":\"{quote.Id}\",\"quoteNumber\":\"{quote.QuoteNumber}\",\"amount\":{quote.TotalAmount}}}"
        );
        await _db.OutboxMessages.AddAsync(outboxMsg, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return quote;
    }
}
