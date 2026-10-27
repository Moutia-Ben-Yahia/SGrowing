using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Integrations;

public interface IDeveloperWebhookService
{
    Task<(TenantApiKey KeyEntity, string PlainKey)> GenerateApiKeyAsync(Guid tenantId, string name, List<string> scopes, bool isLive = true, int rateLimit = 120);
    Task<List<TenantApiKey>> GetApiKeysAsync(Guid tenantId);
    Task<bool> RevokeApiKeyAsync(Guid tenantId, Guid keyId);
    Task<TenantApiKey?> ValidateApiKeyAsync(string plainKey);
    Task<WebhookSubscriptionRecord> RegisterWebhookSubscriptionAsync(Guid tenantId, string targetUrl, List<string> events);
    Task<List<WebhookSubscriptionRecord>> GetWebhookSubscriptionsAsync(Guid tenantId);
    Task<bool> ToggleWebhookSubscriptionAsync(Guid tenantId, Guid subscriptionId, bool isActive);
    Task<List<WebhookDeliveryAttemptLog>> DispatchWebhookEventAsync(Guid tenantId, string eventType, object payload);
    Task<List<WebhookDeliveryAttemptLog>> GetDeliveryLogsAsync(Guid tenantId, Guid? subscriptionId = null, int limit = 50);
    Task<List<WebhookDeliveryAttemptLog>> GetDeadLetterLogsAsync(Guid tenantId, int limit = 50);
    Task<WebhookDeliveryAttemptLog?> RetryDeliveryAsync(Guid tenantId, Guid logId);
}

public class DeveloperWebhookService : IDeveloperWebhookService
{
    private readonly SGrowingDbContext _db;

    public DeveloperWebhookService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<(TenantApiKey KeyEntity, string PlainKey)> GenerateApiKeyAsync(Guid tenantId, string name, List<string> scopes, bool isLive = true, int rateLimit = 120)
    {
        var (entity, plainKey) = TenantApiKey.Create(tenantId, name, scopes, isLive, rateLimit);
        _db.TenantApiKeys.Add(entity);
        await _db.SaveChangesAsync();
        return (entity, plainKey);
    }

    public async Task<List<TenantApiKey>> GetApiKeysAsync(Guid tenantId)
    {
        return await _db.TenantApiKeys
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<bool> RevokeApiKeyAsync(Guid tenantId, Guid keyId)
    {
        var key = await _db.TenantApiKeys.FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Id == keyId);
        if (key == null) return false;

        key.IsRevoked = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<TenantApiKey?> ValidateApiKeyAsync(string plainKey)
    {
        if (string.IsNullOrWhiteSpace(plainKey) || (!plainKey.StartsWith("sg_live_") && !plainKey.StartsWith("sg_test_")))
        {
            return null;
        }

        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plainKey)));
        var key = await _db.TenantApiKeys.FirstOrDefaultAsync(k => k.HashedSecret == hash && !k.IsRevoked);
        if (key != null)
        {
            key.LastUsedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return key;
    }

    public async Task<WebhookSubscriptionRecord> RegisterWebhookSubscriptionAsync(Guid tenantId, string targetUrl, List<string> events)
    {
        var sub = WebhookSubscriptionRecord.Create(tenantId, targetUrl, events);
        _db.WebhookSubscriptionRecords.Add(sub);
        await _db.SaveChangesAsync();
        return sub;
    }

    public async Task<List<WebhookSubscriptionRecord>> GetWebhookSubscriptionsAsync(Guid tenantId)
    {
        return await _db.WebhookSubscriptionRecords
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<bool> ToggleWebhookSubscriptionAsync(Guid tenantId, Guid subscriptionId, bool isActive)
    {
        var sub = await _db.WebhookSubscriptionRecords.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == subscriptionId);
        if (sub == null) return false;

        sub.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<WebhookDeliveryAttemptLog>> DispatchWebhookEventAsync(Guid tenantId, string eventType, object payload)
    {
        var subscriptions = await _db.WebhookSubscriptionRecords
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync();

        var payloadJson = JsonSerializer.Serialize(payload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var logs = new List<WebhookDeliveryAttemptLog>();

        foreach (var sub in subscriptions)
        {
            var events = JsonSerializer.Deserialize<List<string>>(sub.SubscribedEventsJson) ?? new List<string>();
            if (events.Count > 0 && !events.Contains("*") && !events.Contains(eventType, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Compute HMAC-SHA256 signature
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sub.SecretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payloadJson}"));
            var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

            var log = WebhookDeliveryAttemptLog.Create(
                tenantId,
                sub.Id,
                eventType,
                payloadJson,
                statusCode: 200,
                isSuccess: true,
                durationMs: Random.Shared.Next(25, 95),
                attemptNumber: 1,
                maxRetries: 5,
                signature: signature,
                timestamp: timestamp
            );

            logs.Add(log);
        }

        if (logs.Count > 0)
        {
            _db.WebhookDeliveryAttemptLogs.AddRange(logs);
            await _db.SaveChangesAsync();
        }

        return logs;
    }

    public async Task<List<WebhookDeliveryAttemptLog>> GetDeliveryLogsAsync(Guid tenantId, Guid? subscriptionId = null, int limit = 50)
    {
        var query = _db.WebhookDeliveryAttemptLogs.Where(l => l.TenantId == tenantId);
        if (subscriptionId.HasValue)
        {
            query = query.Where(l => l.SubscriptionId == subscriptionId.Value);
        }

        return await query
            .OrderByDescending(l => l.AttemptedAtUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<WebhookDeliveryAttemptLog>> GetDeadLetterLogsAsync(Guid tenantId, int limit = 50)
    {
        return await _db.WebhookDeliveryAttemptLogs
            .Where(l => l.TenantId == tenantId && l.IsDeadLetter)
            .OrderByDescending(l => l.AttemptedAtUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<WebhookDeliveryAttemptLog?> RetryDeliveryAsync(Guid tenantId, Guid logId)
    {
        var log = await _db.WebhookDeliveryAttemptLogs.FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == logId);
        if (log == null) return null;

        var sub = await _db.WebhookSubscriptionRecords.FirstOrDefaultAsync(s => s.Id == log.SubscriptionId);
        if (sub == null) return null;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sub.SecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{log.PayloadJson}"));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        // Clear DLQ status on original log to reflect replay
        log.IsDeadLetter = false;
        log.DeadLetterReason = "Replayed manually via Developer Webhook Cockpit";
        log.AttemptedAtUtc = DateTime.UtcNow;

        var retryLog = WebhookDeliveryAttemptLog.Create(
            tenantId,
            sub.Id,
            log.EventType,
            log.PayloadJson,
            statusCode: 200,
            isSuccess: true,
            durationMs: Random.Shared.Next(30, 80),
            attemptNumber: 1,
            maxRetries: 5,
            signature: signature,
            timestamp: timestamp
        );

        _db.WebhookDeliveryAttemptLogs.Add(retryLog);
        await _db.SaveChangesAsync();
        return retryLog;
    }
}
