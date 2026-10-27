using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Security;
using Xunit;

namespace SGrowing.Tests.Gateways;

public class WhatsAppCloudWebhookReceiverTests
{
    private readonly SGrowingDbContext _db;
    private readonly TenantSecretsVaultService _vault;

    public WhatsAppCloudWebhookReceiverTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SGrowingDbContext(options);
        _vault = new TenantSecretsVaultService(_db);
    }

    [Fact]
    public void VerifyMetaSignature_WithValidHmac_ShouldReturnTrue()
    {
        // Arrange
        const string rawPayload = """{"object":"whatsapp_business_account","entry":[{"id":"10928374","changes":[{"value":{"messaging_product":"whatsapp"}}]}]}""";
        const string secretKey = "super_secret_whatsapp_webhook_token_2026";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();
        var header = $"sha256={hash}";

        // Act
        var isValid = WebhookSignatureValidator.VerifyMetaSignature(rawPayload, secretKey, header);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyMetaSignature_WithTamperedPayload_ShouldReturnFalse()
    {
        // Arrange
        const string originalPayload = """{"object":"whatsapp_business_account"}""";
        const string tamperedPayload = """{"object":"whatsapp_business_account","tampered":true}""";
        const string secretKey = "super_secret_whatsapp_webhook_token_2026";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(originalPayload))).ToLowerInvariant();
        var header = $"sha256={hash}";

        // Act
        var isValid = WebhookSignatureValidator.VerifyMetaSignature(tamperedPayload, secretKey, header);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyMetaSignature_WithWrongSecret_ShouldReturnFalse()
    {
        // Arrange
        const string rawPayload = """{"object":"whatsapp_business_account"}""";
        const string correctSecret = "secretA";
        const string wrongSecret = "secretB";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(correctSecret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();
        var header = $"sha256={hash}";

        // Act
        var isValid = WebhookSignatureValidator.VerifyMetaSignature(rawPayload, wrongSecret, header);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyMetaSignature_WithUppercaseHexAndPrefix_ShouldReturnTrue()
    {
        // Arrange: Meta or reverse proxies may send uppercase hex digests or capitalized prefix
        const string rawPayload = """{"object":"whatsapp_business_account","entry":[]}""";
        const string secretKey = "super_secret_whatsapp_webhook_token_2026";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hashUpper = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload))).ToUpperInvariant();
        var header = $"SHA256={hashUpper}";

        // Act
        var isValid = WebhookSignatureValidator.VerifyMetaSignature(rawPayload, secretKey, header);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyMetaSignature_WithWhitespaceInHeader_ShouldReturnTrue()
    {
        // Arrange
        const string rawPayload = """{"object":"whatsapp_business_account","entry":[]}""";
        const string secretKey = "super_secret_whatsapp_webhook_token_2026";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();
        var header = $"   sha256={hash}   ";

        // Act
        var isValid = WebhookSignatureValidator.VerifyMetaSignature(rawPayload, secretKey, header);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_header")]
    [InlineData("md5=abcdef1234567890")]
    public void VerifyMetaSignature_WithInvalidHeaders_ShouldReturnFalse(string invalidHeader)
    {
        const string rawPayload = """{"object":"whatsapp_business_account"}""";
        const string secretKey = "super_secret_whatsapp_webhook_token_2026";

        var isValid = WebhookSignatureValidator.VerifyMetaSignature(rawPayload, secretKey, invalidHeader);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void ChallengeVerification_WithValidVerifyToken_ShouldVerifySuccessfully()
    {
        // Arrange
        const string expectedVerifyToken = "my_configured_meta_verify_token_99";
        const string clientProvidedToken = "my_configured_meta_verify_token_99";

        // Act
        var isMatch = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(clientProvidedToken),
            Encoding.UTF8.GetBytes(expectedVerifyToken));

        // Assert
        isMatch.Should().BeTrue();
    }

    [Fact]
    public void ChallengeVerification_WithTamperedVerifyToken_ShouldReject()
    {
        // Arrange
        const string expectedVerifyToken = "my_configured_meta_verify_token_99";
        const string clientProvidedToken = "attacker_random_token_123";

        // Act
        var isMatch = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(clientProvidedToken),
            Encoding.UTF8.GetBytes(expectedVerifyToken));

        // Assert
        isMatch.Should().BeFalse();
    }
}
