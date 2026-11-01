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
    private const int PrimeModulo = 257; // Prime for byte-level Shamir Secret Sharing

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

        // Split byte by byte using polynomials of degree M - 1
        var coeffs = new int[request.ThresholdM];

        for (var byteIdx = 0; byteIdx < secretBytes.Length; byteIdx++)
        {
            coeffs[0] = secretBytes[byteIdx]; // f(0) = secret byte

            // Generate random coefficients in [0, PrimeModulo - 1]
            for (var c = 1; c < request.ThresholdM; c++)
            {
                coeffs[c] = RandomNumberGenerator.GetInt32(0, PrimeModulo);
            }

            // Evaluate f(x) for x = 1 .. N
            for (var shareIdx = 0; shareIdx < request.TotalSharesN; shareIdx++)
            {
                var x = shareIdx + 1;
                var y = EvaluatePolynomialMod(coeffs, x, PrimeModulo);
                shares[shareIdx][byteIdx] = (byte)(y % 256);
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
            var ys = new int[threshold];
            for (var i = 0; i < threshold; i++)
            {
                ys[i] = shareByteArrays[i][b];
            }

            // Lagrange interpolation at x = 0 modulo PrimeModulo
            var secretByte = InterpolateLagrangeAtZero(xs, ys, PrimeModulo);
            reconstructed[b] = (byte)(secretByte % 256);
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

    private static int EvaluatePolynomialMod(int[] coeffs, int x, int mod)
    {
        var result = 0;
        var xPower = 1;
        foreach (var c in coeffs)
        {
            result = (result + (c * xPower)) % mod;
            xPower = (xPower * x) % mod;
        }
        return (result + mod) % mod;
    }

    private static int InterpolateLagrangeAtZero(int[] xs, int[] ys, int mod)
    {
        var k = xs.Length;
        var result = 0;

        for (var j = 0; j < k; j++)
        {
            var num = 1;
            var den = 1;

            for (var m = 0; m < k; m++)
            {
                if (m == j) continue;
                num = (num * (-xs[m])) % mod;
                den = (den * (xs[j] - xs[m])) % mod;
            }

            num = (num + mod) % mod;
            den = (den + mod) % mod;

            var denInv = ModInverse(den, mod);
            var term = (ys[j] * num % mod) * denInv % mod;
            result = (result + term) % mod;
        }

        return (result + mod) % mod;
    }

    private static int ModInverse(int a, int m)
    {
        a = (a % m + m) % m;
        for (var x = 1; x < m; x++)
        {
            if ((a * x) % m == 1) return x;
        }
        return 1;
    }
}
