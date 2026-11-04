using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Integrations;

public class DeveloperWebhooksTests
{
    private readonly SGrowingDbContext _db;
    private readonly DeveloperWebhookService _service;

    public DeveloperWebhooksTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new DeveloperWebhookService(_db);
    }

    [Fact]
    public async Task ApiKey_Generate_Validate_And_Revoke_ShouldWorkCorrectly()
    {
        var tenantId = Guid.NewGuid();

        // 1. Generate key
        var (keyEntity, plainKey) = await _service.GenerateApiKeyAsync(
            tenantId, "ERP Automation Key", new List<string> { "orders:read", "orders:write" }, isLive: true, rateLimit: 300);

        Assert.NotNull(keyEntity);
        Assert.StartsWith("sg_live_", plainKey);
        Assert.Equal("ERP Automation Key", keyEntity.Name);
        Assert.Equal(300, keyEntity.RateLimitPerMinute);

        // 2. Validate key
        var validated = await _service.ValidateApiKeyAsync(plainKey);
        Assert.NotNull(validated);
        Assert.Equal(keyEntity.Id, validated.Id);
        Assert.NotNull(validated.LastUsedAtUtc);

        // 3. Invalid key check
        var invalid = await _service.ValidateApiKeyAsync("sg_live_fake_key_12345");
        Assert.Null(invalid);

        // 4. Revoke key
        var revoked = await _service.RevokeApiKeyAsync(tenantId, keyEntity.Id);
        Assert.True(revoked);

        var afterRevoke = await _service.ValidateApiKeyAsync(plainKey);
        Assert.Null(afterRevoke);
    }

    [Fact]
    public async Task Webhook_Subscription_And_Dispatch_ShouldRecordDeliveryLogs()
    {
        var tenantId = Guid.NewGuid();

        // 1. Register subscription
        var sub = await _service.RegisterWebhookSubscriptionAsync(
            tenantId,
            "https://erp.client-logistics.tn/webhooks/sgrowing",
            new List<string> { "order.delivered", "booking.confirmed" }
        );

        Assert.NotNull(sub);
        Assert.StartsWith("whsec_", sub.SecretKey);

        // 2. Dispatch matching event ("order.delivered")
        var orderPayload = new
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-TN-9901",
            TotalAmount = 145.50m,
            DeliveredAtUtc = DateTime.UtcNow
        };

        var logs = await _service.DispatchWebhookEventAsync(tenantId, "order.delivered", orderPayload);

        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal("order.delivered", log.EventType);
        Assert.Equal(200, log.ResponseStatusCode);
        Assert.True(log.IsSuccess);

        // 3. Dispatch non-matching event ("invoice.paid") -> should not dispatch
        var otherLogs = await _service.DispatchWebhookEventAsync(tenantId, "invoice.paid", new { InvoiceId = "INV-1" });
        Assert.Empty(otherLogs);

        // 4. Test retry delivery
        var retry = await _service.RetryDeliveryAsync(tenantId, log.Id);
        Assert.NotNull(retry);
        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public async Task GetApiKeys_ReturnsKeysForTenantInDescendingOrder()
    {
        var tenantId = Guid.NewGuid();
        var (k1, _) = await _service.GenerateApiKeyAsync(tenantId, "Key 1", new List<string> { "read" });
        var (k2, _) = await _service.GenerateApiKeyAsync(tenantId, "Key 2", new List<string> { "write" });

        var keys = await _service.GetApiKeysAsync(tenantId);
        Assert.Equal(2, keys.Count);
        Assert.Equal(k2.Id, keys[0].Id);
        Assert.Equal(k1.Id, keys[1].Id);
    }

    [Fact]
    public async Task RevokeApiKey_WhenKeyDoesNotExist_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        var result = await _service.RevokeApiKeyAsync(tenantId, Guid.NewGuid());
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("invalid_prefix_key_12345")]
    public async Task ValidateApiKey_WithMalformedOrEmptyKey_ReturnsNull(string? key)
    {
        var result = await _service.ValidateApiKeyAsync(key!);
        Assert.Null(result);
    }

    [Fact]
    public async Task ToggleWebhookSubscription_EnablesAndDisablesSubscription()
    {
        var tenantId = Guid.NewGuid();
        var sub = await _service.RegisterWebhookSubscriptionAsync(
            tenantId, "https://example.com/hook", new List<string> { "order.*" });

        Assert.True(sub.IsActive);

        var toggledOff = await _service.ToggleWebhookSubscriptionAsync(tenantId, sub.Id, false);
        Assert.True(toggledOff);

        var subs = await _service.GetWebhookSubscriptionsAsync(tenantId);
        Assert.False(subs[0].IsActive);

        var toggledMissing = await _service.ToggleWebhookSubscriptionAsync(tenantId, Guid.NewGuid(), true);
        Assert.False(toggledMissing);
    }

    [Fact]
    public async Task GetDeadLetterLogs_ReturnsOnlyLogsMarkedAsDeadLetter()
    {
        var tenantId = Guid.NewGuid();
        var sub = await _service.RegisterWebhookSubscriptionAsync(
            tenantId, "https://example.com/dlq", new List<string> { "*" });

        var log1 = WebhookDeliveryAttemptLog.Create(tenantId, sub.Id, "user.created", "{}", 500, false, 100, 5, 5, "sig", 12345);
        log1.IsDeadLetter = true;
        log1.DeadLetterReason = "Exceeded max retries (5)";

        var log2 = WebhookDeliveryAttemptLog.Create(tenantId, sub.Id, "user.updated", "{}", 200, true, 50, 1, 5, "sig", 12346);

        _db.WebhookDeliveryAttemptLogs.AddRange(log1, log2);
        await _db.SaveChangesAsync();

        var dlq = await _service.GetDeadLetterLogsAsync(tenantId);
        Assert.Single(dlq);
        Assert.Equal(log1.Id, dlq[0].Id);
    }

    [Fact]
    public async Task RetryDelivery_WhenLogNotFound_ReturnsNull()
    {
        var result = await _service.RetryDeliveryAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }
}
