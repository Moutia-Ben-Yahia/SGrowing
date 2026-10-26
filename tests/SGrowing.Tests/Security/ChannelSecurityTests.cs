using System.Security.Cryptography;
using System.Text;
using SGrowing.Api.Modules.Security;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Security;

public class ChannelSecurityTests
{
    private readonly AesEncryptionService _aesService;

    public ChannelSecurityTests()
    {
        _aesService = new AesEncryptionService();
    }

    [Fact]
    public void AesGcm_EncryptAndDecrypt_RoundtripsSuccessfully()
    {
        var sensitiveToken = "EAAOx99SecretMetaAccessToken_123456789!@#$%^&*()";
        var payload = _aesService.Encrypt(sensitiveToken);

        Assert.False(string.IsNullOrEmpty(payload.CiphertextBase64));
        Assert.False(string.IsNullOrEmpty(payload.NonceBase64));
        Assert.False(string.IsNullOrEmpty(payload.TagBase64));
        Assert.NotEqual(sensitiveToken, payload.CiphertextBase64);

        var decrypted = _aesService.Decrypt(payload.CiphertextBase64, payload.NonceBase64, payload.TagBase64);
        Assert.Equal(sensitiveToken, decrypted);
    }

    [Fact]
    public void AesGcm_WithDifferentKey_FailsDecryption()
    {
        var originalSecret = "TopSecretWebhookSigningKey";
        var key1 = AesEncryptionService.GenerateRandomKey();
        var key2 = AesEncryptionService.GenerateRandomKey();

        var payload = _aesService.Encrypt(originalSecret, key1);

        Assert.ThrowsAny<CryptographicException>(() =>
        {
            _aesService.Decrypt(payload.CiphertextBase64, payload.NonceBase64, payload.TagBase64, key2);
        });
    }

    [Fact]
    public void MetaSignature_ValidatesCorrectly_AndRejectsTamperedPayload()
    {
        var appSecret = "meta_app_secret_998877";
        var rawBody = "{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\"12345\"}]}";

        // Calculate valid signature
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var validHeader = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var isValid = WebhookSignatureValidator.VerifyMetaSignature(rawBody, appSecret, validHeader);
        Assert.True(isValid);

        // Tampered body
        var tamperedBody = rawBody + " ";
        var isTamperedValid = WebhookSignatureValidator.VerifyMetaSignature(tamperedBody, appSecret, validHeader);
        Assert.False(isTamperedValid);

        // Wrong secret
        var isWrongSecretValid = WebhookSignatureValidator.VerifyMetaSignature(rawBody, "wrong_secret", validHeader);
        Assert.False(isWrongSecretValid);
    }

    [Fact]
    public void TwilioSignature_ValidatesCorrectly_AndRejectsTamperedParams()
    {
        var authToken = "1234567890abcdef1234567890abcdef";
        var requestUrl = "https://api.sgrowing.com/api/webhooks/voice/inbound";
        var postParams = new Dictionary<string, string>
        {
            ["CallSid"] = "CA12345",
            ["From"] = "+15551234567",
            ["To"] = "+15559876543"
        };

        // Twilio signature calculation: URL + sorted key+value
        var sb = new StringBuilder(requestUrl);
        foreach (var kvp in postParams.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append(kvp.Key).Append(kvp.Value);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        var validTwilioSig = Convert.ToBase64String(hash);

        var isValid = WebhookSignatureValidator.VerifyTwilioSignature(requestUrl, postParams, authToken, validTwilioSig);
        Assert.True(isValid);

        // Tampered parameter
        var tamperedParams = new Dictionary<string, string>(postParams)
        {
            ["From"] = "+15550000000"
        };
        var isTamperedValid = WebhookSignatureValidator.VerifyTwilioSignature(requestUrl, tamperedParams, authToken, validTwilioSig);
        Assert.False(isTamperedValid);
    }

    [Fact]
    public void TelegramSecretToken_MatchesConstantTime()
    {
        var secret = "TgSecretToken_987654321_Valid";
        Assert.True(WebhookSignatureValidator.VerifyTelegramSecretToken(secret, secret));
        Assert.False(WebhookSignatureValidator.VerifyTelegramSecretToken("WrongToken", secret));
        Assert.False(WebhookSignatureValidator.VerifyTelegramSecretToken(null, secret));
    }

    [Fact]
    public void DiscordTimestamp_EnforcesReplayLimit()
    {
        var body = "{\"type\":1}";
        var freshTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var fake128HexSig = new string('a', 128);

        // Fresh request
        Assert.True(WebhookSignatureValidator.VerifyDiscordTimestampAndPayload(body, freshTimestamp, fake128HexSig));

        // Replay request from 10 minutes ago
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        Assert.False(WebhookSignatureValidator.VerifyDiscordTimestampAndPayload(body, expiredTimestamp, fake128HexSig));

        // Malformed signature (not 128 hex chars)
        Assert.False(WebhookSignatureValidator.VerifyDiscordTimestampAndPayload(body, freshTimestamp, "short_sig"));
    }

    [Fact]
    public void ChannelCredential_DetectsExpiryCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var credentialValid = ChannelCredential.Create(
            tenantId, "WhatsAppCloud", "enc_access", "enc_secret", "nonce", "tag",
            expiresAtUtc: DateTime.UtcNow.AddDays(30));

        var credentialExpired = ChannelCredential.Create(
            tenantId, "Discord", "enc_access", "enc_secret", "nonce", "tag",
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-5));

        Assert.False(credentialValid.IsExpired());
        Assert.True(credentialExpired.IsExpired());
    }
}
