using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

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
    private readonly IConfiguration? _configuration;

    public StripePaymentWebhookHandler(
        SGrowingDbContext db,
        ILogger<StripePaymentWebhookHandler>? logger = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
    }

    public static bool VerifyStripeSignature(
        string payload,
        string? signatureHeader,
        string webhookSecret,
        int toleranceSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(webhookSecret))
        {
            return false;
        }

        var elements = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? timestampStr = null;
        var signatures = new List<string>();

        foreach (var element in elements)
        {
            var parts = element.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var val = parts[1].Trim();

            if (key == "t")
            {
                timestampStr = val;
            }
            else if (key == "v1")
            {
                signatures.Add(val);
            }
        }

        if (string.IsNullOrEmpty(timestampStr) || !long.TryParse(timestampStr, out var timestamp) || signatures.Count == 0)
        {
            return false;
        }

        if (toleranceSeconds > 0)
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(nowUnix - timestamp) > toleranceSeconds)
            {
                return false;
            }
        }

        var signedPayload = $"{timestamp}.{payload}";
        var keyBytes = Encoding.UTF8.GetBytes(webhookSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
        var hashBytes = HMACSHA256.HashData(keyBytes, payloadBytes);
        var expectedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex);

        foreach (var sig in signatures)
        {
            var sigBytes = Encoding.UTF8.GetBytes(sig.ToLowerInvariant());
            if (sigBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(sigBytes, expectedBytes))
            {
                return true;
            }
        }

        return false;
    }

    public Task<StripeWebhookProcessResult> ProcessStripeEventAsync(
        Guid tenantId,
        string jsonPayload,
        CancellationToken cancellationToken = default)
        => ProcessStripeEventAsync(tenantId, jsonPayload, stripeSignature: null, cancellationToken);

    public async Task<StripeWebhookProcessResult> ProcessStripeEventAsync(
        Guid tenantId,
        string jsonPayload,
        string? stripeSignature,
        CancellationToken cancellationToken = default)
    {
        var webhookSecret = _configuration?["Commerce:Stripe:WebhookSecret"];

        // If webhook secret is configured, enforce cryptographic signature verification
        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            if (string.IsNullOrWhiteSpace(stripeSignature) || !VerifyStripeSignature(jsonPayload, stripeSignature, webhookSecret))
            {
                _logger?.LogWarning("Security Audit: Stripe webhook signature verification failed for tenant {TenantId}. Mismatched or missing signature.", tenantId);
                return new StripeWebhookProcessResult
                {
                    Handled = false,
                    EventType = "signature_verification_failed"
                };
            }
        }

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
