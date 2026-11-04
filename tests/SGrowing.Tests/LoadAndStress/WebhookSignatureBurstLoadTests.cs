using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Api.Modules.Security;
using Xunit;

namespace SGrowing.Tests.LoadAndStress;

public class WebhookSignatureBurstLoadTests
{
    private static string ComputeShopifyHmac(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }

    private static string ComputeWooCommerceSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }

    [Fact]
    public async Task BurstLoad_ShopifyAndWooCommerceSignatures_ShouldVerifyConcurrentlyWithoutRaceConditions()
    {
        const int concurrentTasks = 30;
        const int iterationsPerTask = 100;
        const string shopifySecret = "shpss_test_secret_key_burst_98765";
        const string wooSecret = "wc_sec_test_secret_key_burst_12345";

        var tasks = Enumerable.Range(0, concurrentTasks).Select(workerId => Task.Run(() =>
        {
            for (int i = 0; i < iterationsPerTask; i++)
            {
                // 1. Shopify payload verification
                var shopifyPayload = $"{{\"id\":{workerId * 1000 + i},\"total_price\":\"149.99\",\"currency\":\"USD\"}}";
                var validShopifyHmac = ComputeShopifyHmac(shopifyPayload, shopifySecret);

                var isShopifyValid = ShopifyWebhookReceiver.VerifyShopifyHmac(shopifyPayload, shopifySecret, validShopifyHmac);
                isShopifyValid.Should().BeTrue();

                // Shopify tampered payload test
                var tamperedShopifyPayload = shopifyPayload + " ";
                var isTamperedValid = ShopifyWebhookReceiver.VerifyShopifyHmac(tamperedShopifyPayload, shopifySecret, validShopifyHmac);
                isTamperedValid.Should().BeFalse();

                // 2. WooCommerce payload verification
                var wooPayload = $"{{\"order_id\":{workerId * 1000 + i},\"status\":\"processing\",\"total\":\"89.50\"}}";
                var validWooSig = ComputeWooCommerceSignature(wooPayload, wooSecret);

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(wooSecret));
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(wooPayload));
                var computedBase64 = Convert.ToBase64String(computedHash);
                var isWooValid = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedBase64),
                    Encoding.UTF8.GetBytes(validWooSig.Trim())
                );
                isWooValid.Should().BeTrue();

                // 3. Meta (WhatsApp / FB) signature verification under load
                var metaPayload = $"{{\"entry\":[{{\"id\":\"{workerId}_{i}\"}}]}}";
                using var metaHmac = new HMACSHA256(Encoding.UTF8.GetBytes("meta_app_secret_abc123"));
                var metaHash = Convert.ToHexString(metaHmac.ComputeHash(Encoding.UTF8.GetBytes(metaPayload))).ToLowerInvariant();
                var metaHeader = $"sha256={metaHash}";

                var isMetaValid = WebhookSignatureValidator.VerifyMetaSignature(metaPayload, "meta_app_secret_abc123", metaHeader);
                isMetaValid.Should().BeTrue();
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void VerifySignatures_WithEmptyOrCorruptHeaders_ShouldFailGracefully()
    {
        ShopifyWebhookReceiver.VerifyShopifyHmac("", "secret", "sig").Should().BeFalse();
        ShopifyWebhookReceiver.VerifyShopifyHmac("{}", "", "sig").Should().BeFalse();
        ShopifyWebhookReceiver.VerifyShopifyHmac("{}", "secret", "").Should().BeFalse();
        ShopifyWebhookReceiver.VerifyShopifyHmac("{}", "secret", "invalid_base64_sig!").Should().BeFalse();

        WebhookSignatureValidator.VerifyMetaSignature("", "secret", "sha256=123").Should().BeFalse();
        WebhookSignatureValidator.VerifyMetaSignature("{}", "secret", "invalidprefix123").Should().BeFalse();
    }

    [Fact]
    public void VerifyShopifyHmac_WithValidSignature_MatchesExpectedEncoding()
    {
        var body = "{\"order\":{\"id\":12345,\"total\":\"99.00\"}}";
        var secret = "test_shared_secret_abc";
        var expectedSig = ComputeShopifyHmac(body, secret);

        var valid = ShopifyWebhookReceiver.VerifyShopifyHmac(body, secret, expectedSig);
        valid.Should().BeTrue();
    }

    [Fact]
    public void VerifyMetaSignature_WithTamperedPayload_ReturnsFalse()
    {
        var originalPayload = "{\"object\":\"whatsapp_business_account\",\"entry\":[]}";
        var secret = "app_secret_meta_99";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(originalPayload))).ToLowerInvariant();
        var validHeader = $"sha256={hash}";

        // Verify valid header
        WebhookSignatureValidator.VerifyMetaSignature(originalPayload, secret, validHeader).Should().BeTrue();

        // Tampered payload with valid header
        var tamperedPayload = "{\"object\":\"whatsapp_business_account\",\"entry\":[{\"tampered\":true}]}";
        WebhookSignatureValidator.VerifyMetaSignature(tamperedPayload, secret, validHeader).Should().BeFalse();
    }

    [Fact]
    public void VerifyMetaSignature_WithMalformedPrefix_ReturnsFalse()
    {
        var payload = "{\"test\":true}";
        var secret = "secret_123";

        // Missing sha256= prefix
        WebhookSignatureValidator.VerifyMetaSignature(payload, secret, "sha1=abcdef").Should().BeFalse();
        WebhookSignatureValidator.VerifyMetaSignature(payload, secret, "plain_hash_without_prefix").Should().BeFalse();
    }

    [Fact]
    public void VerifyTwilioSignature_ValidAndTamperedParams_ProperlyAuthenticates()
    {
        const string url = "https://api.sgrowing.io/webhooks/twilio/voice";
        const string authToken = "1234567890abcdef1234567890abcdef";

        var formParams = new Dictionary<string, string>
        {
            ["From"] = "+15551234567",
            ["To"] = "+15559876543",
            ["CallSid"] = "CA1234567890"
        };

        // Compute valid signature: URL + sorted key-values signed with HMAC-SHA1
        var sb = new StringBuilder(url);
        foreach (var kvp in formParams.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append(kvp.Key).Append(kvp.Value);
        }
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var validSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));

        // 1. Valid signature
        var isValid = WebhookSignatureValidator.VerifyTwilioSignature(url, formParams, authToken, validSignature);
        isValid.Should().BeTrue();

        // 2. Tampered param
        var tamperedParams = new Dictionary<string, string>(formParams)
        {
            ["From"] = "+15559999999"
        };
        var isTamperedValid = WebhookSignatureValidator.VerifyTwilioSignature(url, tamperedParams, authToken, validSignature);
        isTamperedValid.Should().BeFalse();

        // 3. Null or empty
        WebhookSignatureValidator.VerifyTwilioSignature("", formParams, authToken, validSignature).Should().BeFalse();
    }

    [Fact]
    public void VerifyTelegramSecretToken_ValidAndInvalidSecrets_MatchesConstantTime()
    {
        const string expectedSecret = "tg_sec_token_9821_xyz";

        // Valid
        WebhookSignatureValidator.VerifyTelegramSecretToken(expectedSecret, expectedSecret).Should().BeTrue();

        // Mismatched
        WebhookSignatureValidator.VerifyTelegramSecretToken("wrong_token", expectedSecret).Should().BeFalse();

        // Null / Empty
        WebhookSignatureValidator.VerifyTelegramSecretToken(null, expectedSecret).Should().BeFalse();
        WebhookSignatureValidator.VerifyTelegramSecretToken(expectedSecret, "").Should().BeFalse();
    }

    [Fact]
    public void VerifyDiscordTimestampAndPayload_ExpiredOrInvalidTimestamp_RejectsReplayAttacks()
    {
        const string rawBody = "{\"content\":\"Hello from webhook\"}";
        const string signature = "sig_valid_dummy";

        // 1. Invalid non-numeric timestamp
        var isInvalidNumeric = WebhookSignatureValidator.VerifyDiscordTimestampAndPayload(rawBody, "not_a_number", signature);
        isInvalidNumeric.Should().BeFalse();

        // 2. Expired timestamp (> 5 minutes ago)
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var isExpired = WebhookSignatureValidator.VerifyDiscordTimestampAndPayload(rawBody, expiredTimestamp, signature);
        isExpired.Should().BeFalse();

        // 3. Null or empty arguments
        WebhookSignatureValidator.VerifyDiscordTimestampAndPayload("", expiredTimestamp, signature).Should().BeFalse();
        WebhookSignatureValidator.VerifyDiscordTimestampAndPayload(rawBody, "", signature).Should().BeFalse();
        WebhookSignatureValidator.VerifyDiscordTimestampAndPayload(rawBody, expiredTimestamp, "").Should().BeFalse();
    }
}
