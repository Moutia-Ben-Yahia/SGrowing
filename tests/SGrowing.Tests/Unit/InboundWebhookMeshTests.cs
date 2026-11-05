using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SGrowing.Api.Modules.Integrations;
using Xunit;

namespace SGrowing.Tests.Unit;

public class InboundWebhookMeshTests
{
    private readonly InboundWebhookMeshService _service = new();
    private const string Secret = "whsec_test_secret_key_12345";

    private static string ComputeHmacHex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string ComputeHmacBase64(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    [Fact]
    public async Task IngestWebhook_ValidStripeSignature_ProcessesSuccessfully()
    {
        var payload = "{\"type\":\"payment_intent.succeeded\",\"data\":{\"amount\":10000}}";
        var hex = ComputeHmacHex(payload, Secret);
        var signatureHeader = $"t=1700000000,v1={hex}";

        var result = await _service.IngestWebhookAsync(
            "stripe",
            signatureHeader,
            payload,
            idempotencyKey: "evt_stripe_101",
            signingSecretOverride: Secret);

        Assert.True(result.Success);
        Assert.Equal("stripe", result.Provider);
        Assert.Equal("payment_intent.succeeded", result.EventType);
        Assert.False(result.IsDuplicate);
    }

    [Fact]
    public async Task IngestWebhook_ValidShopifySignature_ProcessesSuccessfully()
    {
        var payload = "{\"type\":\"orders/create\",\"id\":998877}";
        var signatureHeader = ComputeHmacBase64(payload, Secret);

        var result = await _service.IngestWebhookAsync(
            "shopify",
            signatureHeader,
            payload,
            idempotencyKey: "evt_shopify_202",
            signingSecretOverride: Secret);

        Assert.True(result.Success);
        Assert.Equal("shopify", result.Provider);
        Assert.Equal("orders/create", result.EventType);
    }

    [Fact]
    public async Task IngestWebhook_TamperedSignature_QuarantinesToDlq()
    {
        var payload = "{\"type\":\"charge.dispute.created\"}";
        var tamperedSignature = "v1=0000000000000000000000000000000000000000000000000000000000000000";

        var result = await _service.IngestWebhookAsync(
            "stripe",
            tamperedSignature,
            payload,
            idempotencyKey: "evt_tampered_1",
            signingSecretOverride: Secret);

        Assert.False(result.Success);
        Assert.Equal("SignatureVerificationFailed", result.ErrorReason);

        var dlq = await _service.GetDeadLetterWebhooksAsync("stripe");
        Assert.NotEmpty(dlq);
        Assert.Contains(dlq, r => r.FailureReason == "SignatureVerificationFailed");

        // Replay DLQ item
        var replayed = await _service.ReplayDeadLetterWebhookAsync(dlq[0].RecordId);
        Assert.True(replayed);
    }

    [Fact]
    public async Task IngestWebhook_DuplicateIdempotencyKey_SuppressesReprocessing()
    {
        var payload = "{\"type\":\"order.fulfilled\"}";
        var hex = ComputeHmacHex(payload, Secret);
        var sig = $"v1={hex}";
        var key = "idemp_order_999";

        // First attempt
        var r1 = await _service.IngestWebhookAsync("stripe", sig, payload, key, Secret);
        Assert.True(r1.Success);
        Assert.False(r1.IsDuplicate);

        // Second duplicate attempt
        var r2 = await _service.IngestWebhookAsync("stripe", sig, payload, key, Secret);
        Assert.True(r2.Success);
        Assert.True(r2.IsDuplicate);
        Assert.Equal("DuplicateSuppressed", r2.EventType);
    }
}
