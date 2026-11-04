using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
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

        ShopifyWebhookReceiver.VerifyShopifyHmac(body, secret, validHeader).Should().BeTrue();
        ShopifyWebhookReceiver.VerifyShopifyHmac(body, secret, "invalid_base64_sig").Should().BeFalse();
        ShopifyWebhookReceiver.VerifyShopifyHmac(body + " ", secret, validHeader).Should().BeFalse();
        ShopifyWebhookReceiver.VerifyShopifyHmac("", secret, validHeader).Should().BeFalse();
        ShopifyWebhookReceiver.VerifyShopifyHmac(body, "", validHeader).Should().BeFalse();
        ShopifyWebhookReceiver.VerifyShopifyHmac(body, secret, "").Should().BeFalse();
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
        result.Success.Should().BeTrue();

        var cart = await _db.AbandonedCartRecords.FirstOrDefaultAsync(c => c.ExternalCartId == cartToken);
        cart.Should().NotBeNull();
        cart!.CustomerEmail.Should().Be("buyer@megacorp.com");
        cart.CartTotal.Should().Be(349.99m);
        cart.Status.Should().Be(AbandonedCartStatus.Detected);

        // When order completes later
        var orderJson = $@"{{
            ""id"": 1234567,
            ""checkout_token"": ""{cartToken}""
        }}";

        var orderResult = await _receiver.ProcessWebhookAsync(tenantId, "orders/create", orderJson);
        orderResult.Success.Should().BeTrue();

        var refreshedCart = await _db.AbandonedCartRecords.FindAsync(cart.Id);
        refreshedCart!.Status.Should().Be(AbandonedCartStatus.Recovered);
        refreshedCart.RecoveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessWebhookAsync_IgnoresCompletedCheckout_DoesNotCreateCartRecord()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cartToken = "cart_already_paid_123";

        var checkoutJson = $@"{{
            ""id"": ""{cartToken}"",
            ""email"": ""paid@megacorp.com"",
            ""total_price"": ""199.00"",
            ""abandoned_checkout_url"": ""https://shop.acme.com/checkouts/{cartToken}"",
            ""completed_at"": ""2026-11-04T08:00:00Z""
        }}";

        // Act
        var result = await _receiver.ProcessWebhookAsync(tenantId, "checkouts/create", checkoutJson);

        // Assert
        result.Success.Should().BeTrue();
        var cart = await _db.AbandonedCartRecords.FirstOrDefaultAsync(c => c.ExternalCartId == cartToken);
        cart.Should().BeNull("completed checkouts must not be tracked as abandoned carts");
    }

    [Fact]
    public async Task ProcessWebhookAsync_IgnoresEmptyEmail_DoesNotCreateCartRecord()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cartToken = "cart_anonymous_999";

        var checkoutJson = $@"{{
            ""id"": ""{cartToken}"",
            ""email"": """",
            ""total_price"": ""99.00"",
            ""abandoned_checkout_url"": ""https://shop.acme.com/checkouts/{cartToken}"",
            ""completed_at"": null
        }}";

        // Act
        var result = await _receiver.ProcessWebhookAsync(tenantId, "checkouts/create", checkoutJson);

        // Assert
        result.Success.Should().BeTrue();
        var cart = await _db.AbandonedCartRecords.FirstOrDefaultAsync(c => c.ExternalCartId == cartToken);
        cart.Should().BeNull("carts without customer email cannot be contacted for recovery");
    }

    [Fact]
    public async Task ProcessWebhookAsync_UnknownTopic_ReturnsSuccessWithIgnoredMessage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var payload = "{\"event\":\"customer.created\"}";

        // Act
        var result = await _receiver.ProcessWebhookAsync(tenantId, "customers/create", payload);

        // Assert
        result.Success.Should().BeTrue();
        result.Topic.Should().Be("customers/create");
        result.Message.Should().Be("Ignored topic");
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

        dispatched.Should().ContainSingle();
        dispatched[0].CartId.Should().Be(oldCart.Id);
        dispatched[0].CustomerEmail.Should().Be("charlie@acme.com");
        dispatched[0].FormattedMessage.Should().Contain("SAVE10");

        var refreshedOld = await _db.AbandonedCartRecords.FindAsync(oldCart.Id);
        refreshedOld!.Status.Should().Be(AbandonedCartStatus.RecoveryTriggered);
        refreshedOld.RecoveryAttempts.Should().Be(1);

        var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "AbandonedCartRecoveryDispatchedEvent");
        outbox.Should().NotBeNull();
        outbox!.Payload.Should().Contain("charlie@acme.com");
    }

    [Fact]
    public async Task TriggerRecoveryScanAsync_DoesNotRedispatchAlreadyTriggeredOrRecoveredCarts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var triggeredCart = AbandonedCartRecord.Create(
            tenantId, Guid.NewGuid(), "cart_trig", "alice@example.com", 150m, "https://acme.com/recover/3");
        triggeredCart.MarkRecoveryTriggered();
        typeof(AbandonedCartRecord).GetProperty("DetectedAtUtc")!.SetValue(triggeredCart, DateTime.UtcNow.AddHours(-3));

        var recoveredCart = AbandonedCartRecord.Create(
            tenantId, Guid.NewGuid(), "cart_rec", "bob@example.com", 300m, "https://acme.com/recover/4");
        recoveredCart.MarkRecovered();
        typeof(AbandonedCartRecord).GetProperty("DetectedAtUtc")!.SetValue(recoveredCart, DateTime.UtcNow.AddHours(-3));

        await _db.AbandonedCartRecords.AddRangeAsync(triggeredCart, recoveredCart);
        await _db.SaveChangesAsync();

        // Act
        var dispatched = await _recoveryService.TriggerRecoveryScanAsync(tenantId, TimeSpan.FromMinutes(60));

        // Assert
        dispatched.Should().BeEmpty("only carts with status Detected should be eligible for initial recovery dispatch");
    }

    [Fact]
    public async Task TriggerRecoveryScanAsync_EnforcesTenantIsolation()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var cartA = AbandonedCartRecord.Create(
            tenantA, Guid.NewGuid(), "cart_a", "alice@tenanta.com", 200m, "https://tenanta.com/rec");
        typeof(AbandonedCartRecord).GetProperty("DetectedAtUtc")!.SetValue(cartA, DateTime.UtcNow.AddHours(-2));

        await _db.AbandonedCartRecords.AddAsync(cartA);
        await _db.SaveChangesAsync();

        // Scan executed for Tenant B
        var dispatchedB = await _recoveryService.TriggerRecoveryScanAsync(tenantB, TimeSpan.FromMinutes(60));
        dispatchedB.Should().BeEmpty();

        var inDbA = await _db.AbandonedCartRecords.FindAsync(cartA.Id);
        inDbA!.Status.Should().Be(AbandonedCartStatus.Detected);
    }
}
