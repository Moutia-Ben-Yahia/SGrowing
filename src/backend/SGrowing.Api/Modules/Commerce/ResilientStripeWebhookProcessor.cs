using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Common.Outbox;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public record StripeWebhookDispatchOutcome(
    bool Success,
    string EventId,
    string EventType,
    string? OrderId = null,
    decimal AmountReceived = 0m,
    Guid? OutboxMessageId = null,
    string? FailureReason = null
);

public interface IResilientStripeWebhookProcessor
{
    Task<StripeWebhookDispatchOutcome> ProcessWebhookAsync(
        Guid tenantId,
        string payloadJson,
        string? signatureHeader,
        string? configuredSecret = null,
        CancellationToken ct = default);
}

public class ResilientStripeWebhookProcessor : IResilientStripeWebhookProcessor
{
    private readonly SGrowingDbContext _db;
    private readonly IOutboxService _outboxService;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<ResilientStripeWebhookProcessor>? _logger;

    public ResilientStripeWebhookProcessor(
        SGrowingDbContext db,
        IOutboxService outboxService,
        IConfiguration? configuration = null,
        ILogger<ResilientStripeWebhookProcessor>? logger = null)
    {
        _db = db;
        _outboxService = outboxService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StripeWebhookDispatchOutcome> ProcessWebhookAsync(
        Guid tenantId,
        string payloadJson,
        string? signatureHeader,
        string? configuredSecret = null,
        CancellationToken ct = default)
    {
        var secret = configuredSecret ?? _configuration?["Commerce:Stripe:WebhookSecret"];

        // 1. Signature Verification with Replay Attack Prevention
        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (string.IsNullOrWhiteSpace(signatureHeader) ||
                !StripePaymentWebhookHandler.VerifyStripeSignature(payloadJson, signatureHeader, secret, toleranceSeconds: 300))
            {
                _logger?.LogWarning("Security Rejection: Stripe webhook signature verification failed for tenant {TenantId}.", tenantId);
                return new StripeWebhookDispatchOutcome(
                    Success: false,
                    EventId: "unknown",
                    EventType: "invalid_signature",
                    FailureReason: "HMAC-SHA256 signature mismatch or timestamp outside 300s replay tolerance window.");
            }
        }

        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var eventId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
        var eventType = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "unknown" : "unknown";

        // 2. Idempotency Check via Transactional Outbox correlation
        var alreadyProcessed = await _db.TransactionalOutboxMessages.AnyAsync(
            m => m.TenantId == tenantId && m.EventType == $"Stripe.{eventType}" && m.CorrelationId == eventId,
            cancellationToken: ct);

        if (alreadyProcessed)
        {
            _logger?.LogInformation("Idempotent Ignore: Stripe event {EventId} already processed for tenant {TenantId}.", eventId, tenantId);
            return new StripeWebhookDispatchOutcome(
                Success: true,
                EventId: eventId,
                EventType: eventType,
                FailureReason: "Duplicate event suppressed via idempotency check.");
        }

        string? orderId = null;
        decimal amount = 0m;

        if (root.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("object", out var objEl))
        {
            if (objEl.TryGetProperty("amount_total", out var amtEl) || objEl.TryGetProperty("amount", out amtEl))
            {
                amount = amtEl.GetDecimal() / 100m; // Convert cents to currency units
            }

            if (objEl.TryGetProperty("metadata", out var metaEl))
            {
                if (metaEl.TryGetProperty("order_id", out var ordEl) || metaEl.TryGetProperty("quote_id", out ordEl))
                {
                    orderId = ordEl.GetString();
                }
            }
        }

        // 3. Atomically enqueue event to Transactional Outbox
        var outboxMessage = await _outboxService.EnqueueAsync(
            tenantId: tenantId,
            eventType: $"Stripe.{eventType}",
            payload: new
            {
                EventId = eventId,
                EventType = eventType,
                TenantId = tenantId,
                OrderId = orderId,
                Amount = amount,
                ProcessedAtUtc = DateTime.UtcNow
            },
            correlationId: eventId,
            cancellationToken: ct);

        _logger?.LogInformation("Successfully ingested Stripe webhook {EventType} (ID: {EventId}) -> Outbox #{OutboxId}", eventType, eventId, outboxMessage.Id);

        return new StripeWebhookDispatchOutcome(
            Success: true,
            EventId: eventId,
            EventType: eventType,
            OrderId: orderId,
            AmountReceived: amount,
            OutboxMessageId: outboxMessage.Id);
    }
}
