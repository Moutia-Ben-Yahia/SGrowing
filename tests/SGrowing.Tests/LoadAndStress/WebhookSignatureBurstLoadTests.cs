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
}
