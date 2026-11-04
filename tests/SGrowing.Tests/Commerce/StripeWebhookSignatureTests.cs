using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class StripeWebhookSignatureTests
{
    private readonly SGrowingDbContext _db;

    public StripeWebhookSignatureTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"StripeWebhookDb_{Guid.NewGuid()}")
            .Options;
        _db = new SGrowingDbContext(options);
    }

    [Fact]
    public void VerifyStripeSignature_WithValidSignature_ReturnsTrue()
    {
        var secret = "whsec_test_secret_1234567890abcdef";
        var payload = "{\"id\":\"evt_123\",\"type\":\"checkout.session.completed\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var signedPayload = $"{timestamp}.{payload}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var validHex = Convert.ToHexString(hash).ToLowerInvariant();

        var header = $"t={timestamp},v1={validHex}";

        var result = StripePaymentWebhookHandler.VerifyStripeSignature(payload, header, secret);
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyStripeSignature_WithMismatchedSignature_ReturnsFalse()
    {
        var secret = "whsec_test_secret_1234567890abcdef";
        var payload = "{\"id\":\"evt_123\",\"type\":\"checkout.session.completed\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var badHex = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

        var header = $"t={timestamp},v1={badHex}";

        var result = StripePaymentWebhookHandler.VerifyStripeSignature(payload, header, secret);
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyStripeSignature_WithExpiredTimestamp_ReturnsFalse()
    {
        var secret = "whsec_test_secret_1234567890abcdef";
        var payload = "{\"id\":\"evt_123\",\"type\":\"checkout.session.completed\"}";
        // 10 minutes in the past (tolerance is 300s = 5m)
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        var signedPayload = $"{oldTimestamp}.{payload}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var validHex = Convert.ToHexString(hash).ToLowerInvariant();

        var header = $"t={oldTimestamp},v1={validHex}";

        var result = StripePaymentWebhookHandler.VerifyStripeSignature(payload, header, secret, toleranceSeconds: 300);
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyStripeSignature_WithMissingOrMalformedHeader_ReturnsFalse()
    {
        var secret = "whsec_test_secret_1234567890abcdef";
        var payload = "{\"id\":\"evt_123\"}";

        StripePaymentWebhookHandler.VerifyStripeSignature(payload, null, secret).Should().BeFalse();
        StripePaymentWebhookHandler.VerifyStripeSignature(payload, "", secret).Should().BeFalse();
        StripePaymentWebhookHandler.VerifyStripeSignature(payload, "invalid_header", secret).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessStripeEventAsync_WhenSecretConfiguredAndSignatureMismatched_RejectsWithHandledFalse()
    {
        var secret = "whsec_secret_configured_xyz";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Commerce:Stripe:WebhookSecret"] = secret
            })
            .Build();

        var handler = new StripePaymentWebhookHandler(_db, NullLogger<StripePaymentWebhookHandler>.Instance, config);
        var tenantId = Guid.NewGuid();
        var payload = "{\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"amount\":1000}}}";

        var result = await handler.ProcessStripeEventAsync(tenantId, payload, "t=12345,v1=invalid_signature");

        result.Handled.Should().BeFalse();
        result.EventType.Should().Be("signature_verification_failed");
    }

    [Fact]
    public async Task ProcessStripeEventAsync_WhenSecretConfiguredAndSignatureMissing_RejectsWithHandledFalse()
    {
        var secret = "whsec_secret_configured_xyz";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Commerce:Stripe:WebhookSecret"] = secret
            })
            .Build();

        var handler = new StripePaymentWebhookHandler(_db, NullLogger<StripePaymentWebhookHandler>.Instance, config);
        var tenantId = Guid.NewGuid();
        var payload = "{\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"amount\":1000}}}";

        var result = await handler.ProcessStripeEventAsync(tenantId, payload, stripeSignature: null);

        result.Handled.Should().BeFalse();
        result.EventType.Should().Be("signature_verification_failed");
    }

    [Fact]
    public async Task ProcessStripeEventAsync_WhenSecretConfiguredAndSignatureValid_SuccessfullyProcesses()
    {
        var secret = "whsec_valid_secret_789";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Commerce:Stripe:WebhookSecret"] = secret
            })
            .Build();

        var handler = new StripePaymentWebhookHandler(_db, NullLogger<StripePaymentWebhookHandler>.Instance, config);
        var tenantId = Guid.NewGuid();

        var quote = QuoteProposal.Create(tenantId, Guid.NewGuid(), "QT-SEC-001");
        quote.TotalAmount = 250m;
        _db.QuoteProposals.Add(quote);
        await _db.SaveChangesAsync();

        var payload = $"{{\"type\":\"checkout.session.completed\",\"data\":{{\"object\":{{\"amount_total\":25000,\"metadata\":{{\"quoteId\":\"{quote.Id}\"}}}}}}}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var signature = $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";

        var result = await handler.ProcessStripeEventAsync(tenantId, payload, signature);

        result.Handled.Should().BeTrue();
        result.OrderId.Should().Be("QT-SEC-001");
        result.AmountReceived.Should().Be(250m);
    }

    [Fact]
    public void VerifyStripeSignature_WithEmptySecret_ReturnsFalse()
    {
        var payload = "{\"id\":\"evt_123\"}";
        var header = "t=12345,v1=abcde";

        StripePaymentWebhookHandler.VerifyStripeSignature(payload, header, "").Should().BeFalse();
        StripePaymentWebhookHandler.VerifyStripeSignature(payload, header, "   ").Should().BeFalse();
    }
}
