using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Security;
using Xunit;

namespace SGrowing.Tests.Security;

public class TenantSecretsVaultServiceTests
{
    private readonly SGrowingDbContext _db;
    private readonly TenantSecretsVaultService _vault;

    public TenantSecretsVaultServiceTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SGrowingDbContext(options);
        _vault = new TenantSecretsVaultService(_db);
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldPreservePlaintext_WithAesGcm()
    {
        // Arrange
        const string secretText = "EAABwzLIXnjYBO12345MetaAppSecretTokenUltraSecure";

        // Act
        var payload = _vault.Encrypt(secretText);
        var decrypted = _vault.Decrypt(payload.CiphertextBase64, payload.NonceBase64, payload.TagBase64);

        // Assert
        payload.CiphertextBase64.Should().NotBeNullOrEmpty();
        payload.NonceBase64.Should().NotBeNullOrEmpty();
        payload.TagBase64.Should().NotBeNullOrEmpty();
        payload.CiphertextBase64.Should().NotBe(secretText);
        decrypted.Should().Be(secretText);
    }

    [Fact]
    public async Task StoreAndGetSecretAsync_ShouldIsolateBetweenTenants()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Act
        await _vault.StoreSecretAsync(tenantA, "StripeKey", "sk_live_tenant_a_123");
        await _vault.StoreSecretAsync(tenantB, "StripeKey", "sk_live_tenant_b_456");

        var secretA = await _vault.GetSecretAsync(tenantA, "StripeKey");
        var secretB = await _vault.GetSecretAsync(tenantB, "StripeKey");
        var secretNonExistent = await _vault.GetSecretAsync(tenantA, "NonExistentKey");

        // Assert
        secretA.Should().Be("sk_live_tenant_a_123");
        secretB.Should().Be("sk_live_tenant_b_456");
        secretNonExistent.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSecretAsync_ShouldRemoveSecretFromVault()
    {
        // Arrange
        var tenant = Guid.NewGuid();
        await _vault.StoreSecretAsync(tenant, "OAuthToken", "ghp_123456789");

        // Act
        var initial = await _vault.GetSecretAsync(tenant, "OAuthToken");
        var removed = await _vault.DeleteSecretAsync(tenant, "OAuthToken");
        var afterDelete = await _vault.GetSecretAsync(tenant, "OAuthToken");

        // Assert
        initial.Should().Be("ghp_123456789");
        removed.Should().BeTrue();
        afterDelete.Should().BeNull();
    }

    [Fact]
    public async Task StoreAndGetCredentialAsync_ShouldStoreInDbWithAesGcmEncryption()
    {
        // Arrange
        var tenant = Guid.NewGuid();
        const string channel = "WhatsAppCloud";
        const string token = "EAABwzLIXnjYBOaccess";
        const string secret = "meta_app_secret_998877";
        const string signing = "custom_signing_key_4433";

        // Act
        await _vault.StoreCredentialAsync(tenant, channel, token, secret, signing);
        var cred = await _vault.GetCredentialAsync(tenant, channel);

        // Assert
        cred.Should().NotBeNull();
        cred!.Value.AccessToken.Should().Be(token);
        cred!.Value.AppSecret.Should().Be(secret);
        cred!.Value.SigningKey.Should().Be(signing);

        // Verify DB entity contains encrypted bytes, not plaintext
        var rawEntity = await _db.ChannelCredentials.FirstOrDefaultAsync(c => c.TenantId == tenant && c.ChannelType == channel);
        rawEntity.Should().NotBeNull();
        rawEntity!.EncryptedAccessToken.Should().NotBe(token);
        rawEntity.EncryptedAppSecret.Should().NotBe(secret);
    }

    [Fact]
    public async Task DeleteSecretAsync_ShouldReturnFalse_WhenSecretDoesNotExist()
    {
        var tenant = Guid.NewGuid();
        var result = await _vault.DeleteSecretAsync(tenant, "NonExistentKey123");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task StoreSecretAsync_ShouldOverwriteExistingValueCleanly()
    {
        var tenant = Guid.NewGuid();
        await _vault.StoreSecretAsync(tenant, "RotatingApiKey", "initial_key_111");
        var val1 = await _vault.GetSecretAsync(tenant, "RotatingApiKey");

        await _vault.StoreSecretAsync(tenant, "RotatingApiKey", "rotated_key_222");
        var val2 = await _vault.GetSecretAsync(tenant, "RotatingApiKey");

        val1.Should().Be("initial_key_111");
        val2.Should().Be("rotated_key_222");
    }

    [Fact]
    public async Task GetCredentialAsync_ReturnsNull_WhenChannelNotFound()
    {
        var tenant = Guid.NewGuid();
        var cred = await _vault.GetCredentialAsync(tenant, "NonExistentChannel");
        cred.Should().BeNull();
    }

    [Fact]
    public async Task StoreCredentialAsync_SupportsNullSigningKey()
    {
        var tenant = Guid.NewGuid();
        await _vault.StoreCredentialAsync(tenant, "TelegramBot", "bot_token_abc", "bot_secret_xyz", null);

        var cred = await _vault.GetCredentialAsync(tenant, "TelegramBot");
        cred.Should().NotBeNull();
        cred!.Value.AccessToken.Should().Be("bot_token_abc");
        cred.Value.AppSecret.Should().Be("bot_secret_xyz");
        cred.Value.SigningKey.Should().BeNull();
    }
}
