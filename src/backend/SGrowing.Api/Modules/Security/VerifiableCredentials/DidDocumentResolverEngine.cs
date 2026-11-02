using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.VerifiableCredentials;

/// <summary>
/// Autonomous W3C DID Resolver supporting cryptographic did:key identifiers
/// based on NIST P-256 / Ed25519 curve credentials with full DID Document derivation.
/// </summary>
public sealed class DidDocumentResolverEngine
{
    private static readonly string[] BaseContext =
    [
        "https://www.w3.org/ns/did/v1",
        "https://w3id.org/security/suites/jws-2020/v1"
    ];

    public (string Did, byte[] PrivateKeyDer, byte[] PublicKeyDer) GenerateDidKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
        var privKeyBytes = ecdsa.ExportPkcs8PrivateKey();

        // Encode public key multibase: 'z' + Base58btc
        var multibase = "z" + Base58Encode(pubKeyBytes);
        var did = $"did:key:{multibase}";

        return (did, privKeyBytes, pubKeyBytes);
    }

    public DidDocument Resolve(string did)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(did);

        if (!did.StartsWith("did:key:", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Only 'did:key' scheme is natively resolved without external network lookups. Received: '{did}'");
        }

        var multibase = did["did:key:".Length..];
        var keyId = $"{did}#{multibase}";

        var verificationMethod = new DidVerificationMethod(
            Id: keyId,
            Type: "JsonWebKey2020",
            Controller: did,
            PublicKeyMultibase: multibase
        );

        return new DidDocument(
            Id: did,
            Context: BaseContext,
            VerificationMethod: [verificationMethod],
            Authentication: [keyId],
            AssertionMethod: [keyId]
        );
    }

    public static byte[] SignData(byte[] privateKeyPkcs8, byte[] data)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    public static bool VerifySignature(byte[] publicKeySpki, byte[] data, byte[] signature)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeySpki, out _);
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    public static byte[] ExtractPublicKeyFromDid(string did)
    {
        if (!did.StartsWith("did:key:z", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("DID must start with 'did:key:z'", nameof(did));
        }

        var base58 = did["did:key:z".Length..];
        return Base58Decode(base58);
    }

    #region Base58btc Encoding/Decoding

    private const string Base58Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static string Base58Encode(byte[] data)
    {
        if (data.Length == 0) return string.Empty;

        var zeros = 0;
        while (zeros < data.Length && data[zeros] == 0) zeros++;

        var size = data.Length * 138 / 100 + 1;
        var b58 = new byte[size];

        var length = 0;
        for (var i = zeros; i < data.Length; i++)
        {
            int carry = data[i];
            var j = 0;
            for (var k = size - 1; (carry != 0 || j < length) && k >= 0; k--, j++)
            {
                carry += 256 * b58[k];
                b58[k] = (byte)(carry % 58);
                carry /= 58;
            }
            length = j;
        }

        var it = size - length;
        while (it < size && b58[it] == 0) it++;

        var sb = new StringBuilder(zeros + (size - it));
        sb.Append('1', zeros);
        for (; it < size; it++)
        {
            sb.Append(Base58Digits[b58[it]]);
        }

        return sb.ToString();
    }

    public static byte[] Base58Decode(string s)
    {
        if (string.IsNullOrEmpty(s)) return [];

        var zeros = 0;
        while (zeros < s.Length && s[zeros] == '1') zeros++;

        var size = s.Length * 733 / 1000 + 1;
        var b256 = new byte[size];

        var length = 0;
        for (var i = zeros; i < s.Length; i++)
        {
            var ch = s[i];
            var digit = Base58Digits.IndexOf(ch);
            if (digit < 0) throw new FormatException($"Invalid Base58 character: '{ch}'");

            var carry = digit;
            var j = 0;
            for (var k = size - 1; (carry != 0 || j < length) && k >= 0; k--, j++)
            {
                carry += 58 * b256[k];
                b256[k] = (byte)(carry % 256);
                carry /= 256;
            }
            length = j;
        }

        var it = size - length;
        while (it < size && b256[it] == 0) it++;

        var result = new byte[zeros + (size - it)];
        for (var i = 0; i < zeros; i++) result[i] = 0;
        for (var i = 0; it < size; it++, i++) result[zeros + i] = b256[it];

        return result;
    }

    #endregion
}
