using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Integrations;

public record ApiKeyValidationResult(
    bool IsValid,
    Guid TenantId,
    string KeyName,
    List<string> AllowedScopes,
    string? ErrorMessage
);

public interface IDeveloperGatewayService
{
    Task<(TenantApiKey ApiKey, string PlainSecretKey)> CreateApiKeyAsync(
        Guid tenantId,
        string name,
        List<string> scopes,
        bool isLive = true,
        int rateLimit = 120);
    Task<ApiKeyValidationResult> ValidateApiKeyAsync(string plainKey);
    Task<WebhookSubscriptionRecord> RegisterWebhookAsync(
        Guid tenantId,
        string targetUrl,
        List<string> eventTypes);
    Task<List<WebhookDeliveryAttemptLog>> DispatchWebhookEventAsync(
        Guid tenantId,
        string eventType,
        object payload);
    Task<WebhookDeliveryAttemptLog> ReplayWebhookAttemptAsync(Guid tenantId, Guid attemptLogId);
    Task<List<TenantApiKey>> GetApiKeysAsync(Guid tenantId);
    Task<List<WebhookSubscriptionRecord>> GetWebhooksAsync(Guid tenantId);
}

public class DeveloperGatewayService : IDeveloperGatewayService
{
    private readonly SGrowingDbContext _db;

    public DeveloperGatewayService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<(TenantApiKey ApiKey, string PlainSecretKey)> CreateApiKeyAsync(
        Guid tenantId,
        string name,
        List<string> scopes,
        bool isLive = true,
        int rateLimit = 120)
    {
        var (entity, plainKey) = TenantApiKey.Create(tenantId, name, scopes, isLive, rateLimit);
        _db.TenantApiKeys.Add(entity);
        await _db.SaveChangesAsync();
        return (entity, plainKey);
    }

    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string plainKey)
    {
        if (string.IsNullOrWhiteSpace(plainKey) || (!plainKey.StartsWith("sg_live_") && !plainKey.StartsWith("sg_test_")))
        {
            return new ApiKeyValidationResult(false, Guid.Empty, "", new(), "Invalid key format");
        }

        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plainKey)));
        var key = await _db.TenantApiKeys.FirstOrDefaultAsync(k => k.HashedSecret == hash && !k.IsRevoked);

        if (key == null)
        {
            return new ApiKeyValidationResult(false, Guid.Empty, "", new(), "Key not found or revoked");
        }

        key.LastUsedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var scopes = JsonSerializer.Deserialize<List<string>>(key.AllowedScopesJson) ?? new();
        return new ApiKeyValidationResult(true, key.TenantId, key.Name, scopes, null);
    }

    public async Task<WebhookSubscriptionRecord> RegisterWebhookAsync(
        Guid tenantId,
        string targetUrl,
        List<string> eventTypes)
    {
        var sub = WebhookSubscriptionRecord.Create(tenantId, targetUrl, eventTypes);
        _db.WebhookSubscriptionRecords.Add(sub);
        await _db.SaveChangesAsync();
        return sub;
    }

    public async Task<List<WebhookDeliveryAttemptLog>> DispatchWebhookEventAsync(
        Guid tenantId,
        string eventType,
        object payload)
    {
        var subscriptions = await _db.WebhookSubscriptionRecords
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .ToListAsync();

        var payloadJson = JsonSerializer.Serialize(payload);
        var attempts = new List<WebhookDeliveryAttemptLog>();

        foreach (var sub in subscriptions)
        {
            var events = JsonSerializer.Deserialize<List<string>>(sub.SubscribedEventsJson) ?? new();
            if (!events.Contains(eventType) && !events.Contains("*")) continue;

            // Generate HMAC-SHA256 signature header
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sub.SecretKey));
            var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson)));

            // Simulate immediate or HTTP dispatch
            var log = WebhookDeliveryAttemptLog.Create(
                tenantId,
                sub.Id,
                eventType,
                payloadJson,
                200,
                true,
                durationMs: 38
            );
            attempts.Add(log);
            _db.WebhookDeliveryAttemptLogs.Add(log);
        }

        await _db.SaveChangesAsync();
        return attempts;
    }

    public async Task<WebhookDeliveryAttemptLog> ReplayWebhookAttemptAsync(Guid tenantId, Guid attemptLogId)
    {
        var original = await _db.WebhookDeliveryAttemptLogs
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == attemptLogId);

        if (original == null) throw new InvalidOperationException($"Attempt log {attemptLogId} not found");

        var replay = WebhookDeliveryAttemptLog.Create(
            tenantId,
            original.SubscriptionId,
            original.EventType,
            original.PayloadJson,
            200,
            true,
            durationMs: 25
        );

        _db.WebhookDeliveryAttemptLogs.Add(replay);
        await _db.SaveChangesAsync();
        return replay;
    }

    public async Task<List<TenantApiKey>> GetApiKeysAsync(Guid tenantId)
    {
        return await _db.TenantApiKeys
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<List<WebhookSubscriptionRecord>> GetWebhooksAsync(Guid tenantId)
    {
        return await _db.WebhookSubscriptionRecords
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.CreatedAtUtc)
            .ToListAsync();
    }
}
