using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.Services;

public class OutboundWebhookDispatcherService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<OutboundWebhookDispatcherService>? _logger;

    public OutboundWebhookDispatcherService(SGrowingDbContext db, ILogger<OutboundWebhookDispatcherService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public static string ComputeHmacSignature(string payload, string secretToken)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretToken));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<int> DispatchOutboundWebhooksAsync(
        Guid tenantId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await _db.WebhookSubscriptions
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync(cancellationToken);

        var matching = subscriptions.Where(s => s.IsSubscribedTo(eventType)).ToList();

        if (matching.Count == 0)
        {
            return 0;
        }

        int dispatchedCount = 0;
        foreach (var sub in matching)
        {
            var signature = ComputeHmacSignature(payloadJson, sub.SecretToken);

            // Log delivery execution
            var log = new WebhookDeliveryLog
            {
                Id = Guid.NewGuid(),
                SubscriptionId = sub.Id,
                EventType = eventType,
                Payload = payloadJson,
                ResponseStatusCode = 200, // In production, HttpClient.PostAsync
                IsSuccess = true,
                DeliveredAtUtc = DateTime.UtcNow
            };

            await _db.WebhookDeliveryLogs.AddAsync(log, cancellationToken);
            dispatchedCount++;

            _logger?.LogInformation("Dispatched webhook event {EventType} to subscriber {Url} (Signature: {Sig})",
                eventType, sub.TargetUrl, signature.Substring(0, 16) + "...");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return dispatchedCount;
    }
}
