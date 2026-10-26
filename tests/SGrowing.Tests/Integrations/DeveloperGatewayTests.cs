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
}
