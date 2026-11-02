using System.Numerics;
using System.Security.Cryptography;

namespace SGrowing.Api.Modules.Security.HomomorphicAnalytics;

/// <summary>
/// Autonomous Paillier additive homomorphic cryptosystem engine.
/// Allows mathematical sums and scalar multiplications directly over encrypted ciphertexts
/// without decrypting confidential tenant data.
/// </summary>
public sealed class PaillierHomomorphicAnalyticsEngine
{
    public (PaillierPublicKey PublicKey, PaillierPrivateKey PrivateKey) GenerateKeyPair(int bitLength = 512)
    {
        // Generate two distinct primes p and q
        var p = GeneratePrime(bitLength / 2);
        BigInteger q;
        do
        {
            q = GeneratePrime(bitLength / 2);
        } while (p == q);

        var n = p * q;
        var n2 = n * n;
        var g = n + 1; // Standard simplified generator

        // lambda = lcm(p-1, q-1) = (p-1)*(q-1) / gcd(p-1, q-1)
        var pMinus1 = p - 1;
        var qMinus1 = q - 1;
        var gcd = BigInteger.GreatestCommonDivisor(pMinus1, qMinus1);
        var lambda = (pMinus1 * qMinus1) / gcd;

        // mu = (L(g^lambda mod n^2))^-1 mod n
        // Since g = n + 1, g^lambda mod n^2 = (1 + lambda * n) mod n^2
        // L(1 + lambda * n) = lambda
        // Therefore mu = lambda^-1 mod n
        var mu = ModInverse(lambda % n, n);

        var pubKey = new PaillierPublicKey(
            ModulusHex: n.ToString("X"),
            GeneratorHex: g.ToString("X")
        );

        var privKey = new PaillierPrivateKey(
            LambdaHex: lambda.ToString("X"),
            MuHex: mu.ToString("X"),
            PublicKey: pubKey
        );

        return (pubKey, privKey);
    }

    public BigInteger Encrypt(long plaintext, PaillierPublicKey publicKey)
    {
        var n = publicKey.N;
        var n2 = publicKey.N2;

        var m = new BigInteger(plaintext);
        if (m < 0)
        {
            m = (m % n + n) % n;
        }

        // Generate random r in [1, n-1] coprime to n
        BigInteger r;
        do
        {
            r = GenerateRandomLessThan(n);
        } while (BigInteger.GreatestCommonDivisor(r, n) != 1);

        // c = ((1 + m * n) * r^n) mod n^2
        var gm = (1 + (m * n)) % n2;
        var rn = BigInteger.ModPow(r, n, n2);
        var c = (gm * rn) % n2;

        return c;
    }

    public long Decrypt(BigInteger ciphertext, PaillierPrivateKey privateKey)
    {
        var n = privateKey.PublicKey.N;
        var n2 = privateKey.PublicKey.N2;
        var lambda = privateKey.Lambda;
        var mu = privateKey.Mu;

        // u = c^lambda mod n^2
        var u = BigInteger.ModPow(ciphertext, lambda, n2);

        // L(u) = (u - 1) / n
        var l = (u - 1) / n;

        // m = (L(u) * mu) mod n
        var m = (l * mu) % n;

        // Handle negative numbers if m > n/2
        if (m > n / 2)
        {
            m -= n;
        }

        return (long)m;
    }

    public BigInteger HomomorphicAdd(BigInteger c1, BigInteger c2, PaillierPublicKey publicKey)
    {
        // c_sum = c1 * c2 mod n^2
        return (c1 * c2) % publicKey.N2;
    }

    public BigInteger HomomorphicMultiply(BigInteger c, long scalar, PaillierPublicKey publicKey)
    {
        // c_mult = c^k mod n^2
        var k = new BigInteger(scalar);
        return BigInteger.ModPow(c, k, publicKey.N2);
    }

    public BigInteger AggregateSum(IEnumerable<BigInteger> ciphertexts, PaillierPublicKey publicKey)
    {
        var n2 = publicKey.N2;
        BigInteger total = BigInteger.One;

        foreach (var c in ciphertexts)
        {
            total = (total * c) % n2;
        }

        return total;
    }

    private static BigInteger GeneratePrime(int bits)
    {
        var bytes = (bits + 7) / 8;
        var buffer = new byte[bytes];

        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            buffer[^1] |= 0x01; // ensure odd
            buffer[0] |= 0x80;  // ensure high bit set

            var candidate = new BigInteger(buffer, isUnsigned: true, isBigEndian: true);
            if (IsProbablePrime(candidate, 20))
            {
                return candidate;
            }
        }
    }

    private static bool IsProbablePrime(BigInteger source, int certainty)
    {
        if (source == 2 || source == 3)
            return true;
        if (source < 2 || source % 2 == 0)
            return false;

        var d = source - 1;
        var s = 0;

        while (d % 2 == 0)
        {
            d /= 2;
            s += 1;
        }

        for (var i = 0; i < certainty; i++)
        {
            var a = GenerateRandomInRange(2, source - 2);
            var x = BigInteger.ModPow(a, d, source);

            if (x == 1 || x == source - 1)
                continue;

            for (var r = 1; r < s; r++)
            {
                x = BigInteger.ModPow(x, 2, source);
                if (x == 1)
                    return false;
                if (x == source - 1)
                    break;
            }

            if (x != source - 1)
                return false;
        }

        return true;
    }

    private static BigInteger GenerateRandomInRange(BigInteger min, BigInteger max)
    {
        var range = max - min + 1;
        var bytes = range.ToByteArray().Length;
        var buffer = new byte[bytes];

        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            var candidate = new BigInteger(buffer, isUnsigned: true);
            if (candidate < range)
            {
                return min + candidate;
            }
        }
    }

    private static BigInteger GenerateRandomLessThan(BigInteger max)
    {
        return GenerateRandomInRange(1, max - 1);
    }

    private static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        var m0 = m;
        BigInteger y = 0, x = 1;

        if (m == 1)
            return 0;

        while (a > 1)
        {
            var q = a / m;
            var t = m;

            m = a % m;
            a = t;
            t = y;

            y = x - q * y;
            x = t;
        }

        if (x < 0)
            x += m0;

        return x;
    }
}
