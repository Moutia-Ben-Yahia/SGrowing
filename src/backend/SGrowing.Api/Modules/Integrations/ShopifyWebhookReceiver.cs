using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Integrations;

public class ShopifyWebhookResult
{
    public bool Success { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class ShopifyWebhookReceiver
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<ShopifyWebhookReceiver>? _logger;

    public ShopifyWebhookReceiver(SGrowingDbContext db, ILogger<ShopifyWebhookReceiver>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public static bool VerifyShopifyHmac(string rawBody, string secretKey, string hmacHeader)
    {
        if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(hmacHeader))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedBase64 = Convert.ToBase64String(hash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedBase64),
            Encoding.UTF8.GetBytes(hmacHeader.Trim()));
    }

    public async Task<ShopifyWebhookResult> ProcessWebhookAsync(
        Guid tenantId,
        string topic,
        string rawJson,
        CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var connection = await _db.StorefrontConnections
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Platform == StorefrontPlatform.Shopify && s.IsActive, cancellationToken);

        var connectionId = connection?.Id ?? Guid.Empty;

        if (topic.Equals("checkouts/create", StringComparison.OrdinalIgnoreCase) ||
            topic.Equals("checkouts/update", StringComparison.OrdinalIgnoreCase))
        {
            var cartId = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : (root.TryGetProperty("token", out var tokEl) ? tokEl.GetString() ?? "" : "");
            var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() ?? "" : "";
            var phone = root.TryGetProperty("phone", out var phoneEl) ? phoneEl.GetString() : null;
            var completedAt = root.TryGetProperty("completed_at", out var compEl) ? compEl.GetString() : null;
            
            decimal total = 0;
            if (root.TryGetProperty("total_price", out var priceEl) && decimal.TryParse(priceEl.GetString(), out var pVal))
            {
                total = pVal;
            }

            var recoveryUrl = root.TryGetProperty("abandoned_checkout_url", out var urlEl) ? urlEl.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(cartId) && string.IsNullOrEmpty(completedAt) && !string.IsNullOrEmpty(email))
            {
                var existing = await _db.AbandonedCartRecords
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ExternalCartId == cartId, cancellationToken);

                if (existing == null)
                {
                    var cartRecord = AbandonedCartRecord.Create(
                        tenantId,
                        connectionId,
                        cartId,
                        email,
                        total,
                        recoveryUrl,
                        phone,
                        rawJson);

                    await _db.AbandonedCartRecords.AddAsync(cartRecord, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger?.LogInformation("Ingested abandoned checkout {CartId} ({Total:C}) for {Email}", cartId, total, email);
                }
            }

            return new ShopifyWebhookResult
            {
                Success = true,
                Topic = topic,
                ExternalId = cartId,
                Message = "Checkout recorded"
            };
        }
        else if (topic.Equals("orders/create", StringComparison.OrdinalIgnoreCase))
        {
            var orderId = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : "";
            var cartToken = root.TryGetProperty("checkout_token", out var tokEl) ? tokEl.GetString() : null;

            if (!string.IsNullOrEmpty(cartToken))
            {
                var cart = await _db.AbandonedCartRecords
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ExternalCartId == cartToken, cancellationToken);

                if (cart != null)
                {
                    cart.MarkRecovered();
                    await _db.SaveChangesAsync(cancellationToken);
                    _logger?.LogInformation("Cart {CartToken} marked RECOVERED following Order {OrderId}!", cartToken, orderId);
                }
            }

            return new ShopifyWebhookResult
            {
                Success = true,
                Topic = topic,
                ExternalId = orderId,
                Message = "Order processed"
            };
        }

        return new ShopifyWebhookResult
        {
            Success = true,
            Topic = topic,
            ExternalId = "",
            Message = "Ignored topic"
        };
    }
}
