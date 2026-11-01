using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SGrowing.Api.Modules.Security.Vault;

public record EncryptedPayloadDto(
    Guid TenantId,
    string FieldPurpose,
    string EncryptedDekBase64,
    string CiphertextBase64,
    string NonceBase64,
    string TagBase64,
    string KeyId,
    string Algorithm,
    DateTime EncryptedAtUtc
);

public interface IEnvelopeEncryptionService
{
    Task<EncryptedPayloadDto> EncryptSensitiveFieldAsync(Guid tenantId, string plaintext, string fieldPurpose);
    Task<string> DecryptSensitiveFieldAsync(Guid tenantId, EncryptedPayloadDto payload);
}

public class EnvelopeEncryptionService : IEnvelopeEncryptionService
{
    private const string CurrentAlgorithm = "AES-256-GCM-ENVELOPE-V1";
    private const int KeySizeBytes = 32;       // 256 bits
    private const int NonceSizeBytes = 12;     // 96 bits for GCM
    private const int TagSizeBytes = 16;       // 128 bits
    private const int Pbkdf2Iterations = 100_000;

    private readonly string _masterKek;
    private readonly string _keyId;

    public EnvelopeEncryptionService(IConfiguration? configuration = null, string? masterKek = null)
    {
        _masterKek = masterKek
            ?? configuration?["Security:Vault:MasterKek"]
            ?? "SGrowing_Enterprise_Master_KEK_KeyEncryptionKey_2026_SecretVault!";
        _keyId = "vault-kek-v1";
    }

    public Task<EncryptedPayloadDto> EncryptSensitiveFieldAsync(Guid tenantId, string plaintext, string fieldPurpose)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));
        if (string.IsNullOrWhiteSpace(fieldPurpose)) throw new ArgumentException("FieldPurpose must not be empty.", nameof(fieldPurpose));

        // 1. Derive per-tenant Key Encryption Key (KEK) using PBKDF2/HMAC-SHA256
        var tenantKek = DeriveTenantKek(tenantId);

        // 2. Generate a fresh, random 256-bit Data Encryption Key (DEK)
        var dek = RandomNumberGenerator.GetBytes(KeySizeBytes);

        try
        {
            // 3. Encrypt Plaintext with DEK via AES-256-GCM
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var tag = new byte[TagSizeBytes];
            var aad = Encoding.UTF8.GetBytes($"{tenantId:D}:{fieldPurpose.Trim().ToLowerInvariant()}");

            using (var dekGcm = new AesGcm(dek, TagSizeBytes))
            {
                dekGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);
            }

            // 4. Wrap DEK with Tenant KEK via AES-256-GCM
            var wrappedDek = new byte[KeySizeBytes];
            var dekNonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var dekTag = new byte[TagSizeBytes];
            var dekAad = Encoding.UTF8.GetBytes($"{tenantId:D}:DEK_WRAP");

            using (var kekGcm = new AesGcm(tenantKek, TagSizeBytes))
            {
                kekGcm.Encrypt(dekNonce, dek, wrappedDek, dekTag, dekAad);
            }

            // Pack wrapped DEK: dekNonce.wrappedDek.dekTag
            var packedWrappedDek = $"{Convert.ToBase64String(dekNonce)}.{Convert.ToBase64String(wrappedDek)}.{Convert.ToBase64String(dekTag)}";

            var dto = new EncryptedPayloadDto(
                TenantId: tenantId,
                FieldPurpose: fieldPurpose.Trim(),
                EncryptedDekBase64: packedWrappedDek,
                CiphertextBase64: Convert.ToBase64String(ciphertext),
                NonceBase64: Convert.ToBase64String(nonce),
                TagBase64: Convert.ToBase64String(tag),
                KeyId: _keyId,
                Algorithm: CurrentAlgorithm,
                EncryptedAtUtc: DateTime.UtcNow
            );

            return Task.FromResult(dto);
        }
        finally
        {
            // Cryptographically sanitize sensitive memory
            CryptographicOperations.ZeroMemory(dek);
            CryptographicOperations.ZeroMemory(tenantKek);
        }
    }

    public Task<string> DecryptSensitiveFieldAsync(Guid tenantId, EncryptedPayloadDto payload)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (payload.TenantId != tenantId)
            throw new CryptographicException("Cross-tenant decryption violation: Payload tenant does not match requesting tenant.");

        // 1. Derive per-tenant Key Encryption Key (KEK) using PBKDF2/HMAC-SHA256
        var tenantKek = DeriveTenantKek(tenantId);

        // 2. Unwrap DEK using Tenant KEK
        var wrappedParts = payload.EncryptedDekBase64.Split('.');
        if (wrappedParts.Length != 3)
            throw new CryptographicException("Malformed wrapped DEK envelope structure.");

        var dekNonce = Convert.FromBase64String(wrappedParts[0]);
        var wrappedDek = Convert.FromBase64String(wrappedParts[1]);
        var dekTag = Convert.FromBase64String(wrappedParts[2]);
        var dekAad = Encoding.UTF8.GetBytes($"{tenantId:D}:DEK_WRAP");

        var dek = new byte[KeySizeBytes];

        try
        {
            using (var kekGcm = new AesGcm(tenantKek, TagSizeBytes))
            {
                kekGcm.Decrypt(dekNonce, wrappedDek, dekTag, dek, dekAad);
            }

            // 3. Decrypt Ciphertext with unwrapped DEK
            var ciphertext = Convert.FromBase64String(payload.CiphertextBase64);
            var nonce = Convert.FromBase64String(payload.NonceBase64);
            var tag = Convert.FromBase64String(payload.TagBase64);
            var aad = Encoding.UTF8.GetBytes($"{tenantId:D}:{payload.FieldPurpose.Trim().ToLowerInvariant()}");

            var plaintextBytes = new byte[ciphertext.Length];
            using (var dekGcm = new AesGcm(dek, TagSizeBytes))
            {
                dekGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes, aad);
            }

            var plaintext = Encoding.UTF8.GetString(plaintextBytes);
            CryptographicOperations.ZeroMemory(plaintextBytes);
            return Task.FromResult(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
            CryptographicOperations.ZeroMemory(tenantKek);
        }
    }

    private byte[] DeriveTenantKek(Guid tenantId)
    {
        var masterBytes = Encoding.UTF8.GetBytes(_masterKek);
        var salt = tenantId.ToByteArray();

        return Rfc2898DeriveBytes.Pbkdf2(
            password: masterBytes,
            salt: salt,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySizeBytes
        );
    }
}
