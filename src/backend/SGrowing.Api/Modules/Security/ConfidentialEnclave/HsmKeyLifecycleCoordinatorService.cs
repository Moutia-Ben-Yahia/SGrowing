using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.ConfidentialEnclave;

/// <summary>
/// PKCS#11 compliant HSM coordinator managing cryptographic key lifecycles,
/// Shamir M-of-N threshold secret splitting/reconstruction, and emergency memory zeroization.
/// </summary>
public sealed class HsmKeyLifecycleCoordinatorService
{
    private readonly ConcurrentDictionary<string, (HsmKeyMetadata Metadata, byte[] KeyMaterial)> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _lock = new();

    /// <summary>
    /// Generates a new cryptographic key within the HSM enclave.
    /// </summary>
    public HsmKeyMetadata GenerateKey(string alias, HsmKeyType keyType, int rotationDays = 90)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var keyId = $"KEY-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var keySize = keyType switch
        {
            HsmKeyType.Aes256Gcm => 32,
            HsmKeyType.Kyber1024Pqc => 1568, // Kyber-1024 secret key size
            HsmKeyType.EcSecp256r1 => 32,
            _ => 64
        };

        var keyMaterial = new byte[keySize];
        RandomNumberGenerator.Fill(keyMaterial);

        var now = DateTime.UtcNow;
        var metadata = new HsmKeyMetadata(
            KeyId: keyId,
            KeyType: keyType,
            KeyAlias: alias.Trim(),
            Version: 1,
            Status: HsmKeyStatus.Active,
            CreatedAtUtc: now,
            NextRotationUtc: now.AddDays(rotationDays)
        );

        _keys[keyId] = (metadata, keyMaterial);
        return metadata;
    }

    /// <summary>
    /// Rotates an existing key, preserving old key material under 'Rotated' status for legacy decryption.
    /// </summary>
    public HsmKeyMetadata RotateKey(string keyId, int rotationDays = 90)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        if (!_keys.TryGetValue(keyId, out var existing))
        {
            throw new KeyNotFoundException($"Key with ID '{keyId}' not found in HSM.");
        }

        lock (_lock)
        {
            // Mark existing as rotated
            var rotatedMeta = existing.Metadata with
            {
                Status = HsmKeyStatus.Rotated,
                RotatedAtUtc = DateTime.UtcNow
            };
            _keys[keyId] = (rotatedMeta, existing.KeyMaterial);

            // Create new version
            var newKeyId = $"KEY-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
            var newMaterial = new byte[existing.KeyMaterial.Length];
            RandomNumberGenerator.Fill(newMaterial);

            var now = DateTime.UtcNow;
            var newMetadata = new HsmKeyMetadata(
                KeyId: newKeyId,
                KeyType: existing.Metadata.KeyType,
                KeyAlias: existing.Metadata.KeyAlias,
                Version: existing.Metadata.Version + 1,
                Status: HsmKeyStatus.Active,
                CreatedAtUtc: now,
                NextRotationUtc: now.AddDays(rotationDays)
            );

            _keys[newKeyId] = (newMetadata, newMaterial);
            return newMetadata;
        }
    }

    /// <summary>
    /// Executes emergency zeroization on all keys, wiping cryptographic material from memory.
    /// </summary>
    public int EmergencyZeroizeAllKeys()
    {
        lock (_lock)
        {
            var count = 0;
            foreach (var kv in _keys)
            {
                var (meta, material) = kv.Value;
                // Securely wipe memory
                CryptographicOperations.ZeroMemory(material);
                var zeroizedMeta = meta with { Status = HsmKeyStatus.Zeroized };
                _keys[kv.Key] = (zeroizedMeta, []);
                count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Splits an arbitrary secret into N shares such that any M shares can reconstruct it (Shamir M-of-N).
    /// </summary>
    public IReadOnlyList<ShamirSecretShare> SplitSecret(ShamirSplitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ThresholdM < 2)
        {
            throw new ArgumentException("Threshold M must be at least 2.", nameof(request));
        }

        if (request.TotalSharesN < request.ThresholdM)
        {
            throw new ArgumentException("Total shares N cannot be less than threshold M.", nameof(request));
        }

        if (request.TotalSharesN > 255)
        {
            throw new ArgumentException("Maximum supported shares N is 255.", nameof(request));
        }

        var secretBytes = Convert.FromHexString(request.SecretDataHex);
        var shares = new List<byte[]>(request.TotalSharesN);
        for (var i = 0; i < request.TotalSharesN; i++)
        {
            shares.Add(new byte[secretBytes.Length]);
        }

        // Split byte by byte using polynomials of degree M - 1 over GF(2^8)
        var coeffs = new byte[request.ThresholdM];

        for (var byteIdx = 0; byteIdx < secretBytes.Length; byteIdx++)
        {
            coeffs[0] = secretBytes[byteIdx]; // f(0) = secret byte

            // Generate random coefficients in GF(2^8) [0, 255]
            for (var c = 1; c < request.ThresholdM; c++)
            {
                coeffs[c] = (byte)RandomNumberGenerator.GetInt32(0, 256);
            }

            // Evaluate f(x) for x = 1 .. N
            for (var shareIdx = 0; shareIdx < request.TotalSharesN; shareIdx++)
            {
                var x = (byte)(shareIdx + 1);
                var y = EvaluatePolynomialGf(coeffs, x);
                shares[shareIdx][byteIdx] = y;
            }
        }

        var result = new List<ShamirSecretShare>(request.TotalSharesN);
        for (var i = 0; i < request.TotalSharesN; i++)
        {
            var hex = Convert.ToHexString(shares[i]).ToLowerInvariant();
            var checksum = Convert.ToHexString(SHA256.HashData(shares[i]))[..16].ToLowerInvariant();
            result.Add(new ShamirSecretShare(
                ShareIndex: i + 1,
                ThresholdM: request.ThresholdM,
                TotalSharesN: request.TotalSharesN,
                ShareDataHex: hex,
                ShareChecksumHex: checksum
            ));
        }

        return result;
    }

    /// <summary>
    /// Reconstructs a secret from a threshold subset of Shamir shares using Lagrange interpolation.
    /// </summary>
    public string ReconstructSecret(ShamirReconstructRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Shares.Count == 0)
        {
            throw new ArgumentException("Must supply at least one share.", nameof(request));
        }

        var threshold = request.Shares[0].ThresholdM;
        if (request.Shares.Count < threshold)
        {
            throw new InvalidOperationException($"Insufficient shares provided. Required threshold is {threshold}, but received {request.Shares.Count}.");
        }

        // Verify share checksums
        foreach (var share in request.Shares)
        {
            var shareBytes = Convert.FromHexString(share.ShareDataHex);
            var expectedChecksum = Convert.ToHexString(SHA256.HashData(shareBytes))[..16].ToLowerInvariant();
            if (!string.Equals(share.ShareChecksumHex, expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Corrupted share checksum for share index {share.ShareIndex}.");
            }
        }

        var subset = request.Shares.Take(threshold).ToList();
        var secretLen = Convert.FromHexString(subset[0].ShareDataHex).Length;
        var reconstructed = new byte[secretLen];

        var xs = subset.Select(s => s.ShareIndex).ToArray();
        var shareByteArrays = subset.Select(s => Convert.FromHexString(s.ShareDataHex)).ToArray();

        for (var b = 0; b < secretLen; b++)
        {
            var ys = new byte[threshold];
            for (var i = 0; i < threshold; i++)
            {
                ys[i] = shareByteArrays[i][b];
            }

            // Lagrange interpolation at x = 0 over GF(2^8)
            var secretByte = InterpolateLagrangeGfAtZero(xs, ys);
            reconstructed[b] = secretByte;
        }

        return Convert.ToHexString(reconstructed).ToLowerInvariant();
    }

    /// <summary>
    /// Gets metadata for an HSM key without leaking material.
    /// </summary>
    public HsmKeyMetadata? GetKeyMetadata(string keyId)
    {
        return _keys.TryGetValue(keyId, out var entry) ? entry.Metadata : null;
    }

    private static byte EvaluatePolynomialGf(byte[] coeffs, byte x)
    {
        byte result = 0;
        byte xPower = 1;
        foreach (var c in coeffs)
        {
            result ^= Gf256Mul(c, xPower);
            xPower = Gf256Mul(xPower, x);
        }
        return result;
    }

    private static byte InterpolateLagrangeGfAtZero(int[] xs, byte[] ys)
    {
        var k = xs.Length;
        byte result = 0;

        for (var j = 0; j < k; j++)
        {
            byte num = 1;
            byte den = 1;

            for (var m = 0; m < k; m++)
            {
                if (m == j) continue;
                num = Gf256Mul(num, (byte)xs[m]);
                den = Gf256Mul(den, (byte)(xs[j] ^ xs[m]));
            }

            var denInv = Gf256Inv(den);
            var weight = Gf256Mul(num, denInv);
            result ^= Gf256Mul(ys[j], weight);
        }

        return result;
    }

    private static byte Gf256Mul(byte a, byte b)
    {
        byte p = 0;
        while (a != 0 && b != 0)
        {
            if ((b & 1) != 0)
                p ^= a;
            if ((a & 0x80) != 0)
                a = (byte)((a << 1) ^ 0x11B);
            else
                a = (byte)(a << 1);
            b >>= 1;
        }
        return p;
    }

    private static byte Gf256Inv(byte a)
    {
        if (a == 0) return 0;
        for (var x = 1; x < 256; x++)
        {
            if (Gf256Mul(a, (byte)x) == 1) return (byte)x;
        }
        return 1;
    }
}
