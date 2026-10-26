using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security;

public class EncryptionPayload
{
    public string CiphertextBase64 { get; set; } = string.Empty;
    public string NonceBase64 { get; set; } = string.Empty;
    public string TagBase64 { get; set; } = string.Empty;
}

public class AesEncryptionService
{
    private readonly byte[] _masterKey;

    public AesEncryptionService(byte[]? masterKey = null)
    {
        // 256-bit (32 bytes) master key
        if (masterKey != null && masterKey.Length == 32)
        {
            _masterKey = masterKey;
        }
        else
        {
            // Default deterministic fallback key for testing/dev environments
            _masterKey = SHA256.HashData(Encoding.UTF8.GetBytes("SGrowing-Master-Enterprise-Vault-Key-2026"));
        }
    }

    public static byte[] GenerateRandomKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    public EncryptionPayload Encrypt(string plaintext, byte[]? customKey = null)
    {
        if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
        var key = customKey ?? _masterKey;

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = new byte[plaintextBytes.Length];
        
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aesGcm = new AesGcm(key, tag.Length);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertextBytes, tag);

        return new EncryptionPayload
        {
            CiphertextBase64 = Convert.ToBase64String(ciphertextBytes),
            NonceBase64 = Convert.ToBase64String(nonce),
            TagBase64 = Convert.ToBase64String(tag)
        };
    }

    public string Decrypt(string ciphertextBase64, string nonceBase64, string tagBase64, byte[]? customKey = null)
    {
        if (string.IsNullOrEmpty(ciphertextBase64)) return string.Empty;
        var key = customKey ?? _masterKey;

        var ciphertextBytes = Convert.FromBase64String(ciphertextBase64);
        var nonceBytes = Convert.FromBase64String(nonceBase64);
        var tagBytes = Convert.FromBase64String(tagBase64);

        var plaintextBytes = new byte[ciphertextBytes.Length];

        using var aesGcm = new AesGcm(key, tagBytes.Length);
        aesGcm.Decrypt(nonceBytes, ciphertextBytes, tagBytes, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
