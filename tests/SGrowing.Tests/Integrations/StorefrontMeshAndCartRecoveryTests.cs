using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Integrations;

public class StorefrontMeshAndCartRecoveryTests
{
    private readonly SGrowingDbContext _db;
    private readonly ShopifyWebhookReceiver _receiver;
    private readonly AbandonedCartRecoveryService _recoveryService;

    public StorefrontMeshAndCartRecoveryTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _receiver = new ShopifyWebhookReceiver(_db);
        _recoveryService = new AbandonedCartRecoveryService(_db);
    }

    [Fact]
    public void VerifyShopifyHmac_ValidatesAuthenticHeaders()
    {
        var secret = "shopify_shared_webhook_secret_99";
        var body = "{\"id\":987654321,\"email\":\"shopper@example.com\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var validHeader = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));

        Assert.True(ShopifyWebhookReceiver.VerifyShopifyHmac(body, secret, validHeader));
        Assert.False(ShopifyWebhookReceiver.VerifyShopifyHmac(body, secret, "invalid_base64_sig"));
        Assert.False(ShopifyWebhookReceiver.VerifyShopifyHmac(body + " ", secret, validHeader));
    }

    [Fact]
    public async Task ShopifyWebhook_IngestsAbandonedCheckoutAndRecoversOnOrder()
    {
        var tenantId = Guid.NewGuid();
        var cartToken = "cart_tok_998877";

        var checkoutJson = $@"{{
            ""id"": ""{cartToken}"",
            ""email"": ""buyer@megacorp.com"",
            ""total_price"": ""349.99"",
            ""abandoned_checkout_url"": ""https://shop.acme.com/checkouts/{cartToken}"",
            ""completed_at"": null
        }}";

        var result = await _receiver.ProcessWebhookAsync(tenantId, "checkouts/create", checkoutJson);
        Assert.True(result.Success);

        var cart = await _db.AbandonedCartRecords.FirstOrDefaultAsync(c => c.ExternalCartId == cartToken);
        Assert.NotNull(cart);
        Assert.Equal("buyer@megacorp.com", cart.CustomerEmail);
        Assert.Equal(349.99m, cart.CartTotal);
        Assert.Equal(AbandonedCartStatus.Detected, cart.Status);

        // When order completes later
        var orderJson = $@"{{
            ""id"": 1234567,
            ""checkout_token"": ""{cartToken}""
        }}";

        var orderResult = await _receiver.ProcessWebhookAsync(tenantId, "orders/create", orderJson);
        Assert.True(orderResult.Success);

        var refreshedCart = await _db.AbandonedCartRecords.FindAsync(cart.Id);
        Assert.Equal(AbandonedCartStatus.Recovered, refreshedCart!.Status);
        Assert.NotNull(refreshedCart.RecoveredAtUtc);
    }

    [Fact]
    public async Task AbandonedCartRecovery_ScansAndDispatchesRecoveryMessages()
    {
        var tenantId = Guid.NewGuid();
        var oldCart = AbandonedCartRecord.Create(
            tenantId, Guid.NewGuid(), "cart_old", "charlie@acme.com", 199m, "https://acme.com/recover/1");

        // Simulate cart abandoned 2 hours ago
        typeof(AbandonedCartRecord).GetProperty("DetectedAtUtc")!.SetValue(oldCart, DateTime.UtcNow.AddHours(-2));

        var freshCart = AbandonedCartRecord.Create(
            tenantId, Guid.NewGuid(), "cart_fresh", "dan@acme.com", 50m, "https://acme.com/recover/2");
        // Fresh cart abandoned 5 mins ago
        typeof(AbandonedCartRecord).GetProperty("DetectedAtUtc")!.SetValue(freshCart, DateTime.UtcNow.AddMinutes(-5));

        await _db.AbandonedCartRecords.AddRangeAsync(oldCart, freshCart);
        await _db.SaveChangesAsync();

        var dispatched = await _recoveryService.TriggerRecoveryScanAsync(tenantId, TimeSpan.FromMinutes(60));

        Assert.Single(dispatched);
        Assert.Equal(oldCart.Id, dispatched[0].CartId);
        Assert.Equal("charlie@acme.com", dispatched[0].CustomerEmail);
        Assert.Contains("SAVE10", dispatched[0].FormattedMessage);

        var refreshedOld = await _db.AbandonedCartRecords.FindAsync(oldCart.Id);
        Assert.Equal(AbandonedCartStatus.RecoveryTriggered, refreshedOld!.Status);
        Assert.Equal(1, refreshedOld.RecoveryAttempts);

        var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "AbandonedCartRecoveryDispatchedEvent");
        Assert.NotNull(outbox);
        Assert.Contains("charlie@acme.com", outbox.Payload);
    }
}
