using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Security;

public interface ITenantSecretsVaultService
{
    Task StoreSecretAsync(Guid tenantId, string key, string value, CancellationToken ct = default);
    Task<string?> GetSecretAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task<bool> DeleteSecretAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task StoreCredentialAsync(Guid tenantId, string channelType, string accessToken, string appSecret, string? signingKey = null, CancellationToken ct = default);
    Task<(string AccessToken, string AppSecret, string? SigningKey)?> GetCredentialAsync(Guid tenantId, string channelType, CancellationToken ct = default);
    EncryptionPayload Encrypt(string plaintext, byte[]? customKey = null);
    string Decrypt(string ciphertextBase64, string nonceBase64, string tagBase64, byte[]? customKey = null);
}

public class TenantSecretsVaultService : ITenantSecretsVaultService
{
    private readonly SGrowingDbContext _db;
    private readonly AesEncryptionService _encryption;
    private static readonly Dictionary<string, (string Ciphertext, string Nonce, string Tag)> _inMemoryVault = new();

    public TenantSecretsVaultService(SGrowingDbContext db, AesEncryptionService? encryption = null)
    {
        _db = db;
        _encryption = encryption ?? new AesEncryptionService();
    }

    public EncryptionPayload Encrypt(string plaintext, byte[]? customKey = null)
    {
        return _encryption.Encrypt(plaintext, customKey);
    }

    public string Decrypt(string ciphertextBase64, string nonceBase64, string tagBase64, byte[]? customKey = null)
    {
        return _encryption.Decrypt(ciphertextBase64, nonceBase64, tagBase64, customKey);
    }

    public Task StoreSecretAsync(Guid tenantId, string key, string value, CancellationToken ct = default)
    {
        var payload = Encrypt(value);
        var vaultKey = $"{tenantId}:{key}";
        lock (_inMemoryVault)
        {
            _inMemoryVault[vaultKey] = (payload.CiphertextBase64, payload.NonceBase64, payload.TagBase64);
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetSecretAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        var vaultKey = $"{tenantId}:{key}";
        (string Ciphertext, string Nonce, string Tag) entry;
        lock (_inMemoryVault)
        {
            if (!_inMemoryVault.TryGetValue(vaultKey, out entry))
            {
                return Task.FromResult<string?>(null);
            }
        }

        var plaintext = Decrypt(entry.Ciphertext, entry.Nonce, entry.Tag);
        return Task.FromResult<string?>(plaintext);
    }

    public Task<bool> DeleteSecretAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        var vaultKey = $"{tenantId}:{key}";
        bool removed;
        lock (_inMemoryVault)
        {
            removed = _inMemoryVault.Remove(vaultKey);
        }
        return Task.FromResult(removed);
    }

    private static string Pack(EncryptionPayload payload) => $"{payload.CiphertextBase64}:{payload.NonceBase64}:{payload.TagBase64}";

    private string UnpackAndDecrypt(string storedValue, string defaultNonce, string defaultTag)
    {
        if (string.IsNullOrEmpty(storedValue)) return string.Empty;
        var parts = storedValue.Split(':');
        if (parts.Length == 3)
        {
            return Decrypt(parts[0], parts[1], parts[2]);
        }
        return Decrypt(storedValue, defaultNonce, defaultTag);
    }

    public async Task StoreCredentialAsync(
        Guid tenantId,
        string channelType,
        string accessToken,
        string appSecret,
        string? signingKey = null,
        CancellationToken ct = default)
    {
        var encToken = Encrypt(accessToken);
        var encSecret = Encrypt(appSecret);
        var encSigning = signingKey != null ? Pack(Encrypt(signingKey)) : null;

        var packedToken = Pack(encToken);
        var packedSecret = Pack(encSecret);

        var existing = await _db.ChannelCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ChannelType == channelType, ct);

        if (existing == null)
        {
            var cred = ChannelCredential.Create(
                tenantId,
                channelType,
                packedToken,
                packedSecret,
                encToken.NonceBase64,
                encToken.TagBase64,
                encSigning);

            _db.ChannelCredentials.Add(cred);
        }
        else
        {
            existing.EncryptedAccessToken = packedToken;
            existing.EncryptedAppSecret = packedSecret;
            existing.NonceBase64 = encToken.NonceBase64;
            existing.TagBase64 = encToken.TagBase64;
            existing.EncryptedSigningKey = encSigning;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<(string AccessToken, string AppSecret, string? SigningKey)?> GetCredentialAsync(
        Guid tenantId,
        string channelType,
        CancellationToken ct = default)
    {
        var cred = await _db.ChannelCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ChannelType == channelType && c.IsActive, ct);

        if (cred == null)
            return null;

        var token = UnpackAndDecrypt(cred.EncryptedAccessToken, cred.NonceBase64, cred.TagBase64);
        var secret = UnpackAndDecrypt(cred.EncryptedAppSecret, cred.NonceBase64, cred.TagBase64);
        string? signing = null;
        if (!string.IsNullOrEmpty(cred.EncryptedSigningKey))
        {
            signing = UnpackAndDecrypt(cred.EncryptedSigningKey, cred.NonceBase64, cred.TagBase64);
        }

        return (token, secret, signing);
    }
}
