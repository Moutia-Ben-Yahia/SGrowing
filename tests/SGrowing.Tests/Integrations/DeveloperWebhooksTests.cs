using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Integrations;
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
}
