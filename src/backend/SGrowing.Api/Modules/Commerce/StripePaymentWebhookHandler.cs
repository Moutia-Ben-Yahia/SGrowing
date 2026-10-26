using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Commerce;

public class StripeWebhookProcessResult
{
    public bool Handled { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? OrderId { get; set; }
    public decimal AmountReceived { get; set; }
}

public class StripePaymentWebhookHandler
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<StripePaymentWebhookHandler>? _logger;

    public StripePaymentWebhookHandler(SGrowingDbContext db, ILogger<StripePaymentWebhookHandler>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<StripeWebhookProcessResult> ProcessStripeEventAsync(
        Guid tenantId,
        string jsonPayload,
        CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(jsonPayload);
        var root = doc.RootElement;

        var eventType = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "" : "";

        if (eventType == "checkout.session.completed" || eventType == "payment_intent.succeeded")
        {
            var dataObj = root.GetProperty("data").GetProperty("object");
            var metadata = dataObj.TryGetProperty("metadata", out var metaEl) ? metaEl : default;

            string? quoteIdStr = null;
            if (metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("quoteId", out var qIdEl))
            {
                quoteIdStr = qIdEl.GetString();
            }

            decimal amount = 0;
            if (dataObj.TryGetProperty("amount_total", out var amtEl))
            {
                amount = amtEl.GetInt64() / 100m;
            }
            else if (dataObj.TryGetProperty("amount", out var aEl))
            {
                amount = aEl.GetInt64() / 100m;
            }

            if (!string.IsNullOrEmpty(quoteIdStr) && Guid.TryParse(quoteIdStr, out var quoteId))
            {
                var quote = await _db.QuoteProposals
                    .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quoteId, cancellationToken);

                if (quote != null)
                {
                    quote.MarkConvertedToOrder();

                    var outboxEvent = OutboxMessage.Create(
                        tenantId,
                        "OrderPaidEvent",
                        $"{{\"quoteId\":\"{quote.Id}\",\"quoteNumber\":\"{quote.QuoteNumber}\",\"amount\":{amount},\"eventType\":\"{eventType}\"}}"
                    );
                    await _db.OutboxMessages.AddAsync(outboxEvent, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger?.LogInformation("Stripe payment processed for Quote {QuoteNumber}. Amount: {Amount:C}",
                        quote.QuoteNumber, amount);

                    return new StripeWebhookProcessResult
                    {
                        Handled = true,
                        EventType = eventType,
                        OrderId = quote.QuoteNumber,
                        AmountReceived = amount
                    };
                }
            }

            return new StripeWebhookProcessResult
            {
                Handled = true,
                EventType = eventType,
                AmountReceived = amount
            };
        }

        return new StripeWebhookProcessResult
        {
            Handled = false,
            EventType = eventType
        };
    }
}
