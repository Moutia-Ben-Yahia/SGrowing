using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Integrations;
using Xunit;

namespace SGrowing.Tests.Integrations;

public class DeveloperGatewayTests
{
    private readonly SGrowingDbContext _db;
    private readonly DeveloperGatewayService _service;

    public DeveloperGatewayTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new DeveloperGatewayService(_db);
    }

    [Fact]
    public async Task CreateApiKey_AndValidate_AuthenticatesTenantAndScopes()
    {
        var tenantId = Guid.NewGuid();

        // 1. Create API key
        var (apiKey, plainSecret) = await _service.CreateApiKeyAsync(
            tenantId,
            "Shopify Sync Worker Key",
            new() { "orders:read", "orders:write", "couriers:read" },
            isLive: true,
            rateLimit: 300
        );

        Assert.NotNull(apiKey);
        Assert.StartsWith("sg_live_", plainSecret);
        Assert.StartsWith("sg_live_****", apiKey.KeyMasked);

        // 2. Validate valid key
        var validation = await _service.ValidateApiKeyAsync(plainSecret);
        Assert.True(validation.IsValid);
        Assert.Equal(tenantId, validation.TenantId);
        Assert.Contains("orders:write", validation.AllowedScopes);

        // 3. Validate bogus key
        var invalid = await _service.ValidateApiKeyAsync("sg_live_invalid_secret_key_12345");
        Assert.False(invalid.IsValid);
    }

    [Fact]
    public async Task RegisterWebhook_DispatchAndReplay_LogsDeliveries()
    {
        var tenantId = Guid.NewGuid();

        // 1. Register webhook
        var sub = await _service.RegisterWebhookAsync(
            tenantId,
            "https://erp.enterprise-customer.com/webhooks/orders",
            new() { "order.delivered", "order.refused" }
        );

        Assert.NotNull(sub);
        Assert.StartsWith("whsec_", sub.SecretKey);

        // 2. Dispatch event
        var payload = new { orderNumber = "COD-261026-9011", amount = 145.0m, status = "Delivered" };
        var attempts = await _service.DispatchWebhookEventAsync(tenantId, "order.delivered", payload);

        Assert.Single(attempts);
        Assert.Equal(200, attempts[0].ResponseStatusCode);
        Assert.True(attempts[0].IsSuccess);

        // 3. Replay attempt
        var replayed = await _service.ReplayWebhookAttemptAsync(tenantId, attempts[0].Id);
        Assert.NotNull(replayed);
        Assert.True(replayed.IsSuccess);
    }

    [Fact]
    public async Task ValidateApiKey_RejectsKey_WhenPrefixIsNotSgLiveOrSgTest()
    {
        var invalidFormat1 = await _service.ValidateApiKeyAsync("invalid_prefix_12345");
        Assert.False(invalidFormat1.IsValid);
        Assert.Equal("Invalid key format", invalidFormat1.ErrorMessage);

        var invalidFormat2 = await _service.ValidateApiKeyAsync("");
        Assert.False(invalidFormat2.IsValid);
        Assert.Equal("Invalid key format", invalidFormat2.ErrorMessage);
    }

    [Fact]
    public async Task ValidateApiKey_RejectsKey_WhenRevoked()
    {
        var tenantId = Guid.NewGuid();
        var (apiKey, plainSecret) = await _service.CreateApiKeyAsync(
            tenantId,
            "Temporary Key",
            new() { "orders:read" }
        );

        // Revoke key
        apiKey.IsRevoked = true;
        await _db.SaveChangesAsync();

        var result = await _service.ValidateApiKeyAsync(plainSecret);
        Assert.False(result.IsValid);
        Assert.Equal("Key not found or revoked", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateApiKey_TestKey_GeneratesSgTestPrefixAndValidatesSuccessfully()
    {
        var tenantId = Guid.NewGuid();
        var (apiKey, plainSecret) = await _service.CreateApiKeyAsync(
            tenantId,
            "Test Sandbox Key",
            new() { "sandbox:execute" },
            isLive: false
        );

        Assert.StartsWith("sg_test_", plainSecret);
        Assert.StartsWith("sg_test_****", apiKey.KeyMasked);

        var validation = await _service.ValidateApiKeyAsync(plainSecret);
        Assert.True(validation.IsValid);
        Assert.Equal(tenantId, validation.TenantId);
        Assert.Contains("sandbox:execute", validation.AllowedScopes);
    }

    [Fact]
    public async Task DispatchWebhookEvent_MatchesWildcardSubscription()
    {
        var tenantId = Guid.NewGuid();
        await _service.RegisterWebhookAsync(
            tenantId,
            "https://sink.external.com/webhooks/all",
            new() { "*" }
        );

        var payload = new { item = "sku_102", stock = 50 };
        var attempts = await _service.DispatchWebhookEventAsync(tenantId, "inventory.replenished", payload);

        Assert.Single(attempts);
        Assert.True(attempts[0].IsSuccess);
        Assert.Equal("inventory.replenished", attempts[0].EventType);
    }

    [Fact]
    public async Task ReplayWebhookAttempt_ThrowsInvalidOperationException_WhenAttemptNotFound()
    {
        var tenantId = Guid.NewGuid();
        var unknownAttemptId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReplayWebhookAttemptAsync(tenantId, unknownAttemptId));
    }

    [Fact]
    public async Task GetApiKeysAndWebhooks_EnforcesTenantIsolation()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _service.CreateApiKeyAsync(tenantA, "Key A", new() { "scope:a" });
        await _service.CreateApiKeyAsync(tenantB, "Key B", new() { "scope:b" });

        await _service.RegisterWebhookAsync(tenantA, "https://a.com/wh", new() { "event.a" });
        await _service.RegisterWebhookAsync(tenantB, "https://b.com/wh", new() { "event.b" });

        var keysA = await _service.GetApiKeysAsync(tenantA);
        var webhooksA = await _service.GetWebhooksAsync(tenantA);

        Assert.Single(keysA);
        Assert.Equal("Key A", keysA[0].Name);

        Assert.Single(webhooksA);
        Assert.Equal("https://a.com/wh", webhooksA[0].TargetUrl);
    }
}
