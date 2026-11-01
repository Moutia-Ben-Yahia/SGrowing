using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace SGrowing.Api.Modules.Security.Confidential;

/// <summary>
/// Production-grade implementation of the Paillier Additive Homomorphic Cryptosystem.
/// Supports arbitrary-precision arithmetic, signed integers, and multi-party secure aggregation.
/// </summary>
public sealed class PaillierHomomorphicEncryptionEngine : IPaillierHomomorphicEncryptionEngine
{
    private static readonly int[] SmallPrimes = [
        3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97,
        101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199,
        211, 223, 227, 229, 233, 239, 241, 251
    ];

    public PaillierKeyPair GenerateKeyPair(int bitLength = 512)
    {
        if (bitLength < 64)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength), "Modulus bit length must be at least 64 bits.");
        }

        int primeBits = bitLength / 2;

        BigInteger p;
        BigInteger q;
        BigInteger n;
        BigInteger nSquared;
        BigInteger g;
        BigInteger lambda;
        BigInteger mu;

        while (true)
        {
            p = GenerateProbablePrime(primeBits);
            q = GenerateProbablePrime(primeBits);

            while (p == q || BigInteger.Abs(p - q) < (BigInteger.One << (primeBits - 2)))
            {
                q = GenerateProbablePrime(primeBits);
            }

            n = p * q;
            nSquared = n * n;
            g = n + BigInteger.One; // Canonical generator (1 + n)

            BigInteger pMinus1 = p - BigInteger.One;
            BigInteger qMinus1 = q - BigInteger.One;
            BigInteger gcd = BigInteger.GreatestCommonDivisor(pMinus1, qMinus1);
            lambda = (pMinus1 * qMinus1) / gcd;

            // u = g^lambda mod n^2
            BigInteger u = BigInteger.ModPow(g, lambda, nSquared);
            BigInteger lOfU = (u - BigInteger.One) / n;

            if (BigInteger.GreatestCommonDivisor(lOfU, n) == BigInteger.One)
            {
                mu = ModInverse(lOfU, n);
                break;
            }
        }

        var publicKey = new PaillierPublicKey
        {
            ModulusN = ToHex(n),
            ModulusNSquared = ToHex(nSquared),
            GeneratorG = ToHex(g),
            KeySizeBits = bitLength
        };

        var privateKey = new PaillierPrivateKey
        {
            Lambda = ToHex(lambda),
            Mu = ToHex(mu),
            PublicKey = publicKey
        };

        return new PaillierKeyPair
        {
            PublicKey = publicKey,
            PrivateKey = privateKey
        };
    }

    public EncryptedValue Encrypt(long plaintext, PaillierPublicKey publicKey)
    {
        return Encrypt(new BigInteger(plaintext), publicKey);
    }

    public EncryptedValue Encrypt(BigInteger plaintext, PaillierPublicKey publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.ModulusN);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.ModulusNSquared);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.GeneratorG);

        BigInteger n = FromHex(publicKey.ModulusN);
        BigInteger nSquared = FromHex(publicKey.ModulusNSquared);
        BigInteger g = FromHex(publicKey.GeneratorG);

        // Map plaintext into residue class modulo n
        BigInteger m = Mod(plaintext, n);

        // Choose random r in Z*_n
        BigInteger r = GenerateCoprime(n);

        // c = (g^m * r^n) mod n^2
        BigInteger gm = BigInteger.ModPow(g, m, nSquared);
        BigInteger rn = BigInteger.ModPow(r, n, nSquared);
        BigInteger c = Mod(gm * rn, nSquared);

        return new EncryptedValue
        {
            CiphertextHex = ToHex(c),
            ModulusNSquaredHex = publicKey.ModulusNSquared,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public long Decrypt(string ciphertextHex, PaillierPrivateKey privateKey)
    {
        BigInteger m = DecryptBigInteger(ciphertextHex, privateKey);
        BigInteger n = FromHex(privateKey.PublicKey.ModulusN);

        // If decrypted value is in the upper half [n/2, n), it represents a negative value in two's complement modulo n
        if (m > n / 2)
        {
            BigInteger signedVal = m - n;
            return (long)signedVal;
        }

        return (long)m;
    }

    public BigInteger DecryptBigInteger(string ciphertextHex, PaillierPrivateKey privateKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertextHex);
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(privateKey.PublicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey.Lambda);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey.Mu);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey.PublicKey.ModulusN);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey.PublicKey.ModulusNSquared);

        BigInteger c = FromHex(ciphertextHex);
        BigInteger n = FromHex(privateKey.PublicKey.ModulusN);
        BigInteger nSquared = FromHex(privateKey.PublicKey.ModulusNSquared);
        BigInteger lambda = FromHex(privateKey.Lambda);
        BigInteger mu = FromHex(privateKey.Mu);

        // u = c^lambda mod n^2
        BigInteger u = BigInteger.ModPow(c, lambda, nSquared);

        // L(u) = (u - 1) / n
        BigInteger lOfU = (u - BigInteger.One) / n;

        // m = (L(u) * mu) mod n
        BigInteger m = Mod(lOfU * mu, n);
        return m;
    }

    public string AddEncrypted(string ciphertext1Hex, string ciphertext2Hex, PaillierPublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext1Hex);
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext2Hex);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.ModulusNSquared);

        BigInteger c1 = FromHex(ciphertext1Hex);
        BigInteger c2 = FromHex(ciphertext2Hex);
        BigInteger nSquared = FromHex(publicKey.ModulusNSquared);

        // c_sum = (c1 * c2) mod n^2
        BigInteger cSum = Mod(c1 * c2, nSquared);
        return ToHex(cSum);
    }

    public string SubtractEncrypted(string ciphertext1Hex, string ciphertext2Hex, PaillierPublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext1Hex);
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext2Hex);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.ModulusNSquared);

        BigInteger c1 = FromHex(ciphertext1Hex);
        BigInteger c2 = FromHex(ciphertext2Hex);
        BigInteger nSquared = FromHex(publicKey.ModulusNSquared);

        // c_diff = (c1 * c2^(-1)) mod n^2
        BigInteger c2Inv = ModInverse(c2, nSquared);
        BigInteger cDiff = Mod(c1 * c2Inv, nSquared);
        return ToHex(cDiff);
    }

    public string MultiplyScalar(string ciphertextHex, long scalar, PaillierPublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertextHex);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.ModulusNSquared);

        BigInteger c = FromHex(ciphertextHex);
        BigInteger nSquared = FromHex(publicKey.ModulusNSquared);

        if (scalar == 0)
        {
            // Decrypt(c^0 mod n^2) = Decrypt(1) = 0
            return ToHex(BigInteger.One);
        }

        if (scalar > 0)
        {
            BigInteger cProd = BigInteger.ModPow(c, scalar, nSquared);
            return ToHex(cProd);
        }
        else
        {
            // Negative scalar: (c^(-1))^(-scalar) mod n^2
            BigInteger cInv = ModInverse(c, nSquared);
            BigInteger cProd = BigInteger.ModPow(cInv, -scalar, nSquared);
            return ToHex(cProd);
        }
    }

    public string RandomizeCiphertext(string ciphertextHex, PaillierPublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertextHex);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.ModulusN);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey.ModulusNSquared);

        BigInteger c = FromHex(ciphertextHex);
        BigInteger n = FromHex(publicKey.ModulusN);
        BigInteger nSquared = FromHex(publicKey.ModulusNSquared);

        BigInteger r = GenerateCoprime(n);
        BigInteger rn = BigInteger.ModPow(r, n, nSquared);

        BigInteger cRandomized = Mod(c * rn, nSquared);
        return ToHex(cRandomized);
    }

    // ==========================================
    // Mathematical & Cryptographic Helpers
    // ==========================================

    public static BigInteger Mod(BigInteger a, BigInteger m)
    {
        BigInteger r = a % m;
        return r.Sign < 0 ? r + m : r;
    }

    public static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        BigInteger aMod = Mod(a, m);
        if (aMod == BigInteger.Zero)
        {
            throw new InvalidOperationException("Modular inverse does not exist for zero.");
        }

        if (BigInteger.GreatestCommonDivisor(aMod, m) != BigInteger.One)
        {
            throw new InvalidOperationException($"Modular inverse does not exist because gcd({aMod}, {m}) != 1.");
        }

        BigInteger m0 = m;
        BigInteger y = 0, x = 1;

        if (m == 1) return 0;

        while (aMod > 1)
        {
            BigInteger q = aMod / m;
            BigInteger t = m;

            m = aMod % m;
            aMod = t;
            t = y;

            y = x - q * y;
            x = t;
        }

        if (x < 0)
        {
            x += m0;
        }

        return x;
    }

    public static string ToHex(BigInteger value)
    {
        byte[] bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        return bytes.Length == 0 ? "00" : Convert.ToHexString(bytes);
    }

    public static BigInteger FromHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        string normalized = hex.Trim();
        if (normalized.Length % 2 != 0)
        {
            normalized = "0" + normalized;
        }
        byte[] bytes = Convert.FromHexString(normalized);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    private static BigInteger GenerateCoprime(BigInteger n)
    {
        int byteLength = n.ToByteArray().Length;
        byte[] buffer = new byte[byteLength];

        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            BigInteger r = new BigInteger(buffer, isUnsigned: true, isBigEndian: true);
            r = Mod(r, n - BigInteger.One) + BigInteger.One; // In [1, n - 1]

            if (r > BigInteger.One && BigInteger.GreatestCommonDivisor(r, n) == BigInteger.One)
            {
                return r;
            }
        }
    }

    private static BigInteger GenerateProbablePrime(int bitLength)
    {
        if (bitLength < 16) bitLength = 16;
        int byteLength = (bitLength + 7) / 8;
        byte[] bytes = new byte[byteLength];

        while (true)
        {
            RandomNumberGenerator.Fill(bytes);

            // Set top bit to ensure bit length
            int topBitOffset = (bitLength - 1) % 8;
            bytes[0] |= (byte)(1 << topBitOffset);
            // Mask higher bits
            int mask = (1 << (topBitOffset + 1)) - 1;
            bytes[0] &= (byte)mask;
            // Set lowest bit to ensure candidate is odd
            bytes[^1] |= 1;

            BigInteger candidate = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);

            // Fast trial division by small primes
            bool divisible = false;
            foreach (int p in SmallPrimes)
            {
                if (candidate % p == 0 && candidate != p)
                {
                    divisible = true;
                    break;
                }
            }
            if (divisible) continue;

            // Miller-Rabin test
            if (IsProbablePrime(candidate, 20))
            {
                return candidate;
            }
        }
    }

    private static bool IsProbablePrime(BigInteger n, int k = 20)
    {
        if (n <= 1) return false;
        if (n <= 3) return true;
        if (n.IsEven) return false;

        // n - 1 = 2^s * d
        BigInteger d = n - BigInteger.One;
        int s = 0;
        while (d.IsEven)
        {
            d >>= 1;
            s++;
        }

        int byteLength = n.ToByteArray().Length;
        byte[] buffer = new byte[byteLength];

        for (int i = 0; i < k; i++)
        {
            BigInteger a;
            do
            {
                RandomNumberGenerator.Fill(buffer);
                a = new BigInteger(buffer, isUnsigned: true, isBigEndian: true);
                a = Mod(a, n - 3) + 2; // a in [2, n - 2]
            } while (a < 2 || a >= n - 1);

            BigInteger x = BigInteger.ModPow(a, d, n);
            if (x == BigInteger.One || x == n - BigInteger.One)
                continue;

            bool composite = true;
            for (int r = 1; r < s; r++)
            {
                x = BigInteger.ModPow(x, 2, n);
                if (x == n - BigInteger.One)
                {
                    composite = false;
                    break;
                }
            }

            if (composite) return false;
        }

        return true;
    }
}
