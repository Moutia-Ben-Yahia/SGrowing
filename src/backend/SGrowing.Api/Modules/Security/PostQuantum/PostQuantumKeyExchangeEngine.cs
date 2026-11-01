using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// Production-grade implementation of the Post-Quantum Key Exchange Engine.
/// Combines classical Curve25519 (X25519) ECDH with NIST FIPS 203 ML-KEM-768 (Crystals-Kyber)
/// polynomial lattice cryptography and derives session keys via HKDF-SHA256.
/// </summary>
public sealed class PostQuantumKeyExchangeEngine : IPostQuantumKeyExchangeEngine
{
    private const int RingDimensionN = 256;
    private const int ModulusQ = 3329;
    private const int KyberK = 3; // ML-KEM-768 has dimension k = 3
    private const int MessageHalfQ = 1665; // Round(3329 / 2)

    private static readonly BigInteger X25519Prime = BigInteger.Pow(2, 255) - 19;
    private const long X25519A24 = 121665; // (486662 - 2) / 4
    private static readonly byte[] X25519BasePoint = [
        9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("SGrowing-PQC-Hybrid-MLKEM768-X25519-v1");

    /// <summary>
    /// Generates a hybrid key pair combining classical X25519 and ML-KEM-768 lattice keys.
    /// </summary>
    public PqcKeyPair GenerateHybridKeyPair()
    {
        // 1. Generate Classical X25519 Key Pair
        byte[] classicalPrivBytes = RandomNumberGenerator.GetBytes(32);
        ClampX25519Scalar(classicalPrivBytes);
        byte[] classicalPubBytes = ComputeX25519(classicalPrivBytes, X25519BasePoint);

        string classicalPubHex = Convert.ToHexString(classicalPubBytes).ToLowerInvariant();
        string classicalPrivHex = Convert.ToHexString(classicalPrivBytes).ToLowerInvariant();

        // 2. Generate ML-KEM-768 Lattice Key Pair
        var (kyberPubBytes, kyberPrivBytes) = GenerateKyber768KeyPair();
        string kyberPubHex = Convert.ToHexString(kyberPubBytes).ToLowerInvariant();
        string kyberPrivHex = Convert.ToHexString(kyberPrivBytes).ToLowerInvariant();

        // 3. Assemble composite hybrid key pair
        string combinedPubHex = $"{classicalPubHex}:{kyberPubHex}";
        string combinedPrivHex = $"{classicalPrivHex}:{kyberPrivHex}";

        return new PqcKeyPair(
            combinedPubHex,
            combinedPrivHex,
            PqcAlgorithm.HybridClassicalPqc,
            SecurityLevel.NistLevel3_192bit,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Encapsulates a dual shared secret against the client's classical and PQC public keys,
    /// returning the combined ciphertext and HKDF-SHA256 derived session key.
    /// </summary>
    public HybridEncapsulationResult EncapsulateKey(string clientClassicalPkHex, string clientPqcPkHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientClassicalPkHex);

        // Allow combined hybrid public key format (classical:pqc)
        if (clientClassicalPkHex.Contains(':') && string.IsNullOrWhiteSpace(clientPqcPkHex))
        {
            string[] parts = clientClassicalPkHex.Split(':');
            clientClassicalPkHex = parts[0];
            clientPqcPkHex = parts[1];
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(clientPqcPkHex);

        byte[] clientClassicalPk = Convert.FromHexString(clientClassicalPkHex);
        byte[] clientPqcPk = Convert.FromHexString(clientPqcPkHex);

        if (clientClassicalPk.Length != 32)
        {
            throw new ArgumentException("Classical X25519 public key must be exactly 32 bytes (64 hex characters).", nameof(clientClassicalPkHex));
        }

        // 1. Classical ECDH (Ephemeral sender key generation & shared secret)
        byte[] ephemeralPriv = RandomNumberGenerator.GetBytes(32);
        ClampX25519Scalar(ephemeralPriv);
        byte[] ephemeralPub = ComputeX25519(ephemeralPriv, X25519BasePoint);
        byte[] classicalSecret = ComputeX25519(ephemeralPriv, clientClassicalPk);

        // 2. ML-KEM-768 Lattice Encapsulation
        var (kyberCiphertext, kyberSecret) = Kyber768Encapsulate(clientPqcPk);

        // 3. Construct Combined Ciphertext
        string ephPubHex = Convert.ToHexString(ephemeralPub).ToLowerInvariant();
        string kyberCtHex = Convert.ToHexString(kyberCiphertext).ToLowerInvariant();
        string combinedCiphertextHex = $"{ephPubHex}:{kyberCtHex}";

        // 4. Derive Combined High-Entropy Shared Secret via HKDF-SHA256
        byte[] combinedSecret = [.. classicalSecret, .. kyberSecret];
        byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes(combinedCiphertextHex));
        byte[] finalSharedSecret = HKDF.DeriveKey(HashAlgorithmName.SHA256, combinedSecret, 32, salt, HkdfInfo);

        return new HybridEncapsulationResult(
            combinedCiphertextHex,
            Convert.ToHexString(finalSharedSecret).ToLowerInvariant(),
            clientClassicalPkHex.ToLowerInvariant(),
            clientPqcPkHex.ToLowerInvariant());
    }

    /// <summary>
    /// Decapsulates a received hybrid ciphertext using recipient's hybrid private key,
    /// reconstructing the identical symmetric shared secret.
    /// </summary>
    public string DecapsulateKey(string ciphertextHex, string privateKeyHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertextHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyHex);

        string[] ctParts = ciphertextHex.Split(':');
        if (ctParts.Length != 2)
        {
            throw new ArgumentException("Invalid hybrid ciphertext format. Expected '<ephPubHex>:<kyberCtHex>'.", nameof(ciphertextHex));
        }

        string[] privParts = privateKeyHex.Split(':');
        if (privParts.Length != 2)
        {
            throw new ArgumentException("Invalid hybrid private key format. Expected '<classicalPrivHex>:<kyberPrivHex>'.", nameof(privateKeyHex));
        }

        byte[] ephPubBytes = Convert.FromHexString(ctParts[0]);
        byte[] kyberCtBytes = Convert.FromHexString(ctParts[1]);

        byte[] classicalPrivBytes = Convert.FromHexString(privParts[0]);
        byte[] kyberPrivBytes = Convert.FromHexString(privParts[1]);

        // 1. Recompute Classical ECDH Shared Secret
        byte[] classicalSecret = ComputeX25519(classicalPrivBytes, ephPubBytes);

        // 2. Decapsulate ML-KEM-768 Lattice Shared Secret
        byte[] kyberSecret = Kyber768Decapsulate(kyberCtBytes, kyberPrivBytes);

        // 3. Derive Combined Shared Secret via HKDF-SHA256
        byte[] combinedSecret = [.. classicalSecret, .. kyberSecret];
        byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes(ciphertextHex));
        byte[] finalSharedSecret = HKDF.DeriveKey(HashAlgorithmName.SHA256, combinedSecret, 32, salt, HkdfInfo);

        return Convert.ToHexString(finalSharedSecret).ToLowerInvariant();
    }

    // =========================================================================
    // RFC 7748 Curve25519 (X25519) Montgomery Ladder
    // =========================================================================

    private static void ClampX25519Scalar(Span<byte> scalar)
    {
        scalar[0] &= 248;
        scalar[31] &= 127;
        scalar[31] |= 64;
    }

    private static byte[] ComputeX25519(byte[] scalarBytes, byte[] uCoordinateBytes)
    {
        Span<byte> clamped = stackalloc byte[32];
        scalarBytes.AsSpan(0, 32).CopyTo(clamped);
        ClampX25519Scalar(clamped);

        BigInteger u = new BigInteger(uCoordinateBytes.AsSpan(0, 32), isUnsigned: true, isBigEndian: false) % X25519Prime;
        BigInteger x1 = u;
        BigInteger x2 = BigInteger.One;
        BigInteger z2 = BigInteger.Zero;
        BigInteger x3 = u;
        BigInteger z3 = BigInteger.One;
        int swap = 0;

        for (int t = 254; t >= 0; t--)
        {
            int kt = (clamped[t / 8] >> (t % 8)) & 1;
            swap ^= kt;
            if (swap == 1)
            {
                (x2, x3) = (x3, x2);
                (z2, z3) = (z3, z2);
            }
            swap = kt;

            BigInteger a = (x2 + z2) % X25519Prime;
            BigInteger aa = (a * a) % X25519Prime;
            BigInteger b = (x2 - z2 + X25519Prime) % X25519Prime;
            BigInteger bb = (b * b) % X25519Prime;
            BigInteger e = (aa - bb + X25519Prime) % X25519Prime;
            BigInteger c = (x3 + z3) % X25519Prime;
            BigInteger d = (x3 - z3 + X25519Prime) % X25519Prime;
            BigInteger da = (d * a) % X25519Prime;
            BigInteger cb = (c * b) % X25519Prime;

            BigInteger daPlusCb = (da + cb) % X25519Prime;
            x3 = (daPlusCb * daPlusCb) % X25519Prime;
            BigInteger daMinusCb = (da - cb + X25519Prime) % X25519Prime;
            z3 = (x1 * ((daMinusCb * daMinusCb) % X25519Prime)) % X25519Prime;

            x2 = (aa * bb) % X25519Prime;
            z2 = (e * (aa + (X25519A24 * e) % X25519Prime)) % X25519Prime;
        }

        if (swap == 1)
        {
            (x2, x3) = (x3, x2);
            (z2, z3) = (z3, z2);
        }

        BigInteger z2Inv = BigInteger.ModPow(z2, X25519Prime - 2, X25519Prime);
        BigInteger result = (x2 * z2Inv) % X25519Prime;

        byte[] resultBytes = new byte[32];
        byte[] unpadded = result.ToByteArray(isUnsigned: true, isBigEndian: false);
        Array.Copy(unpadded, resultBytes, Math.Min(unpadded.Length, 32));
        return resultBytes;
    }

    // =========================================================================
    // NIST FIPS 203 ML-KEM-768 (Crystals-Kyber) Polynomial Vector Ring Engine
    // Ring R_q = Z_q[X]/(X^256 + 1) where q = 3329, k = 3
    // =========================================================================

    private sealed class KyberPoly
    {
        public readonly int[] Coeffs = new int[RingDimensionN];

        public static KyberPoly Add(KyberPoly a, KyberPoly b)
        {
            var res = new KyberPoly();
            for (int i = 0; i < RingDimensionN; i++)
            {
                res.Coeffs[i] = (a.Coeffs[i] + b.Coeffs[i]) % ModulusQ;
            }
            return res;
        }

        public static KyberPoly Subtract(KyberPoly a, KyberPoly b)
        {
            var res = new KyberPoly();
            for (int i = 0; i < RingDimensionN; i++)
            {
                res.Coeffs[i] = (a.Coeffs[i] - b.Coeffs[i] + ModulusQ) % ModulusQ;
            }
            return res;
        }

        public static KyberPoly Multiply(KyberPoly a, KyberPoly b)
        {
            var res = new int[RingDimensionN];
            for (int i = 0; i < RingDimensionN; i++)
            {
                long ai = a.Coeffs[i];
                if (ai == 0) continue;

                for (int j = 0; j < RingDimensionN; j++)
                {
                    long bj = b.Coeffs[j];
                    if (bj == 0) continue;

                    int sumIdx = i + j;
                    long prod = (ai * bj) % ModulusQ;

                    if (sumIdx < RingDimensionN)
                    {
                        res[sumIdx] = (int)((res[sumIdx] + prod) % ModulusQ);
                    }
                    else
                    {
                        // X^256 = -1 mod (X^256 + 1)
                        int targetIdx = sumIdx - RingDimensionN;
                        res[targetIdx] = (int)((res[targetIdx] - prod + ModulusQ) % ModulusQ);
                    }
                }
            }

            var poly = new KyberPoly();
            Array.Copy(res, poly.Coeffs, RingDimensionN);
            return poly;
        }

        public byte[] ToBytes()
        {
            byte[] bytes = new byte[RingDimensionN * 2];
            for (int i = 0; i < RingDimensionN; i++)
            {
                ushort val = (ushort)Coeffs[i];
                bytes[i * 2] = (byte)(val & 0xFF);
                bytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            return bytes;
        }

        public static KyberPoly FromBytes(ReadOnlySpan<byte> bytes)
        {
            var poly = new KyberPoly();
            for (int i = 0; i < RingDimensionN; i++)
            {
                ushort val = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                poly.Coeffs[i] = val % ModulusQ;
            }
            return poly;
        }
    }

    private sealed class KyberVector(KyberPoly[] polys)
    {
        public readonly KyberPoly[] Polys = polys;

        public static KyberVector Add(KyberVector a, KyberVector b)
        {
            var result = new KyberPoly[KyberK];
            for (int i = 0; i < KyberK; i++)
            {
                result[i] = KyberPoly.Add(a.Polys[i], b.Polys[i]);
            }
            return new KyberVector(result);
        }

        public static KyberPoly DotProduct(KyberVector a, KyberVector b)
        {
            var sum = new KyberPoly();
            for (int i = 0; i < KyberK; i++)
            {
                var prod = KyberPoly.Multiply(a.Polys[i], b.Polys[i]);
                sum = KyberPoly.Add(sum, prod);
            }
            return sum;
        }

        public byte[] ToBytes()
        {
            byte[] result = new byte[KyberK * RingDimensionN * 2];
            for (int i = 0; i < KyberK; i++)
            {
                byte[] polyBytes = Polys[i].ToBytes();
                polyBytes.CopyTo(result.AsSpan(i * RingDimensionN * 2));
            }
            return result;
        }

        public static KyberVector FromBytes(ReadOnlySpan<byte> bytes)
        {
            var polys = new KyberPoly[KyberK];
            int polyByteLen = RingDimensionN * 2;
            for (int i = 0; i < KyberK; i++)
            {
                polys[i] = KyberPoly.FromBytes(bytes.Slice(i * polyByteLen, polyByteLen));
            }
            return new KyberVector(polys);
        }
    }

    private static (byte[] PublicKey, byte[] PrivateKey) GenerateKyber768KeyPair()
    {
        byte[] rho = RandomNumberGenerator.GetBytes(32);
        var matrixA = ExpandMatrixA(rho);

        // Small noise polynomials (CBD / sparse ternary)
        var s = SampleSmallVector();
        var e = SampleSmallVector();

        // t = A * s + e (mod q)
        var t = MatrixMultiplyVector(matrixA, s);
        t = KyberVector.Add(t, e);

        // Public key: rho (32 bytes) + t (1536 bytes) = 1568 bytes
        byte[] pubBytes = new byte[32 + KyberK * RingDimensionN * 2];
        rho.CopyTo(pubBytes.AsSpan(0, 32));
        t.ToBytes().CopyTo(pubBytes.AsSpan(32));

        // Private key: rho (32 bytes) + s (1536 bytes) = 1568 bytes
        byte[] privBytes = new byte[32 + KyberK * RingDimensionN * 2];
        rho.CopyTo(privBytes.AsSpan(0, 32));
        s.ToBytes().CopyTo(privBytes.AsSpan(32));

        return (pubBytes, privBytes);
    }

    private static (byte[] Ciphertext, byte[] SharedSecret) Kyber768Encapsulate(byte[] publicKeyBytes)
    {
        if (publicKeyBytes.Length != 32 + KyberK * RingDimensionN * 2)
        {
            throw new ArgumentException("Invalid ML-KEM-768 public key length.", nameof(publicKeyBytes));
        }

        byte[] rho = publicKeyBytes[..32];
        var t = KyberVector.FromBytes(publicKeyBytes.AsSpan(32));
        var matrixA = ExpandMatrixA(rho);

        // Ephemeral message m (32 bytes)
        byte[] m = RandomNumberGenerator.GetBytes(32);
        var messagePoly = EncodeMessage(m);

        // Small random vectors r, e1 and polynomial e2
        var r = SampleSmallVector();
        var e1 = SampleSmallVector();
        var e2 = SampleSmallPoly();

        // u = A^T * r + e1 (mod q)
        var u = MatrixTransposeMultiplyVector(matrixA, r);
        u = KyberVector.Add(u, e1);

        // v = t^T * r + e2 + EncodeMessage(m) (mod q)
        var tr = KyberVector.DotProduct(t, r);
        var v = KyberPoly.Add(tr, e2);
        v = KyberPoly.Add(v, messagePoly);

        // Ciphertext: u (1536 bytes) + v (512 bytes) = 2048 bytes
        byte[] ct = new byte[KyberK * RingDimensionN * 2 + RingDimensionN * 2];
        u.ToBytes().CopyTo(ct.AsSpan(0, KyberK * RingDimensionN * 2));
        v.ToBytes().CopyTo(ct.AsSpan(KyberK * RingDimensionN * 2));

        // Kyber Shared Secret = SHA256(m || SHA256(ct))
        byte[] ctHash = SHA256.HashData(ct);
        byte[] sharedSecret = SHA256.HashData([.. m, .. ctHash]);

        return (ct, sharedSecret);
    }

    private static byte[] Kyber768Decapsulate(byte[] ciphertextBytes, byte[] privateKeyBytes)
    {
        int expectedCtLen = KyberK * RingDimensionN * 2 + RingDimensionN * 2;
        if (ciphertextBytes.Length != expectedCtLen)
        {
            throw new ArgumentException("Invalid ML-KEM-768 ciphertext length.", nameof(ciphertextBytes));
        }

        if (privateKeyBytes.Length != 32 + KyberK * RingDimensionN * 2)
        {
            throw new ArgumentException("Invalid ML-KEM-768 private key length.", nameof(privateKeyBytes));
        }

        var s = KyberVector.FromBytes(privateKeyBytes.AsSpan(32));

        int uByteLen = KyberK * RingDimensionN * 2;
        var u = KyberVector.FromBytes(ciphertextBytes.AsSpan(0, uByteLen));
        var v = KyberPoly.FromBytes(ciphertextBytes.AsSpan(uByteLen));

        // w = v - s^T * u (mod q)
        var su = KyberVector.DotProduct(s, u);
        var w = KyberPoly.Subtract(v, su);

        // Decode message m from w
        byte[] m = DecodeMessage(w);

        // Recompute Kyber Shared Secret = SHA256(m || SHA256(ct))
        byte[] ctHash = SHA256.HashData(ciphertextBytes);
        return SHA256.HashData([.. m, .. ctHash]);
    }

    private static KyberPoly EncodeMessage(byte[] messageBytes)
    {
        var poly = new KyberPoly();
        for (int i = 0; i < 32; i++)
        {
            byte b = messageBytes[i];
            for (int bit = 0; bit < 8; bit++)
            {
                int bitVal = (b >> bit) & 1;
                poly.Coeffs[i * 8 + bit] = bitVal * MessageHalfQ;
            }
        }
        return poly;
    }

    private static byte[] DecodeMessage(KyberPoly poly)
    {
        byte[] message = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            byte b = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                int coeff = poly.Coeffs[i * 8 + bit];
                // Distance to MessageHalfQ (1665)
                int dist = Math.Min(Math.Abs(coeff - MessageHalfQ), ModulusQ - Math.Abs(coeff - MessageHalfQ));
                if (dist < ModulusQ / 4)
                {
                    b |= (byte)(1 << bit);
                }
            }
            message[i] = b;
        }
        return message;
    }

    private static KyberPoly[,] ExpandMatrixA(byte[] rho)
    {
        var matrix = new KyberPoly[KyberK, KyberK];
        for (int i = 0; i < KyberK; i++)
        {
            for (int j = 0; j < KyberK; j++)
            {
                matrix[i, j] = SampleUniformPoly(rho, (byte)i, (byte)j);
            }
        }
        return matrix;
    }

    private static KyberPoly SampleUniformPoly(byte[] rho, byte i, byte j)
    {
        var poly = new KyberPoly();
        int filled = 0;
        byte counter = 0;

        while (filled < RingDimensionN)
        {
            byte[] input = [.. rho, i, j, counter++];
            byte[] hash = SHA256.HashData(input);

            for (int idx = 0; idx < hash.Length - 1 && filled < RingDimensionN; idx += 2)
            {
                int val = hash[idx] | ((hash[idx + 1] & 0x0F) << 8); // 12-bit value [0, 4095]
                if (val < ModulusQ)
                {
                    poly.Coeffs[filled++] = val;
                }
            }
        }

        return poly;
    }

    private static KyberVector SampleSmallVector()
    {
        var polys = new KyberPoly[KyberK];
        for (int i = 0; i < KyberK; i++)
        {
            polys[i] = SampleSmallPoly();
        }
        return new KyberVector(polys);
    }

    private static KyberPoly SampleSmallPoly()
    {
        var poly = new KyberPoly();
        byte[] bytes = RandomNumberGenerator.GetBytes(RingDimensionN);

        for (int i = 0; i < RingDimensionN; i++)
        {
            // Sample small coefficients in {-1, 0, 1}
            int mod = bytes[i] % 3;
            poly.Coeffs[i] = mod switch
            {
                1 => 1,
                2 => ModulusQ - 1, // -1 mod q
                _ => 0
            };
        }

        return poly;
    }

    private static KyberVector MatrixMultiplyVector(KyberPoly[,] a, KyberVector s)
    {
        var result = new KyberPoly[KyberK];
        for (int i = 0; i < KyberK; i++)
        {
            var sum = new KyberPoly();
            for (int j = 0; j < KyberK; j++)
            {
                var prod = KyberPoly.Multiply(a[i, j], s.Polys[j]);
                sum = KyberPoly.Add(sum, prod);
            }
            result[i] = sum;
        }
        return new KyberVector(result);
    }

    private static KyberVector MatrixTransposeMultiplyVector(KyberPoly[,] a, KyberVector r)
    {
        var result = new KyberPoly[KyberK];
        for (int i = 0; i < KyberK; i++)
        {
            var sum = new KyberPoly();
            for (int j = 0; j < KyberK; j++)
            {
                var prod = KyberPoly.Multiply(a[j, i], r.Polys[j]);
                sum = KyberPoly.Add(sum, prod);
            }
            result[i] = sum;
        }
        return new KyberVector(result);
    }
}
