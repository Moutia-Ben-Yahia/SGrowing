using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.MpcThresholdSigner;

/// <summary>
/// Autonomous Multi-Party Computation (MPC) Threshold Signer engine.
/// Implements t-of-n threshold Schnorr signature generation and verification
/// using Shamir polynomial sharing and Lagrange basis interpolation over finite fields.
/// </summary>
public sealed class ThresholdEcdsaMpcEngine
{
    // 256-bit Safe Prime P = 2*Q + 1 and prime order Q subgroup generator G
    private static readonly BigInteger Q = BigInteger.Parse("08000000000000000000000000000000000000000000000000000000000130C59", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger P = (2 * Q) + 1;
    private static readonly BigInteger G = new(4); // G = 2^2 generates order Q subgroup in Z_P*

    public MpcKeyCluster GenerateThresholdCluster(int thresholdT, int totalN)
    {
        if (thresholdT < 2 || thresholdT > totalN)
        {
            throw new ArgumentException("Threshold t must be >= 2 and <= total participants n.");
        }

        // Generate master secret x in [1, Q-1]
        var masterSecret = GenerateRandomScalar();

        // Generate polynomial coefficients a_1 ... a_{t-1}
        var coefficients = new List<BigInteger> { masterSecret };
        for (var i = 1; i < thresholdT; i++)
        {
            coefficients.Add(GenerateRandomScalar());
        }

        // Compute master public key Y = G^x mod P
        var masterPubKey = BigInteger.ModPow(G, masterSecret, P);

        // Generate n shares: s_i = f(i) mod Q for i = 1 .. n
        var shares = new List<MpcKeyShare>();
        for (var i = 1; i <= totalN; i++)
        {
            var xi = new BigInteger(i);
            var shareVal = EvaluatePolynomial(coefficients, xi, Q);

            shares.Add(new MpcKeyShare(
                ShareIndex: i,
                ShareValueHex: shareVal.ToString("X"),
                ParticipantId: $"SIGNER-NODE-{i:D2}"
            ));
        }

        return new MpcKeyCluster(
            ClusterId: $"MPC-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ThresholdT: thresholdT,
            TotalParticipantsN: totalN,
            MasterPublicKeyHex: masterPubKey.ToString("X"),
            Shares: shares,
            CreatedAtUtc: DateTime.UtcNow
        );
    }

    public ThresholdSignature SignMessage(
        string masterPublicKeyHex,
        string messageText,
        IReadOnlyList<MpcKeyShare> signerShares)
    {
        ArgumentNullException.ThrowIfNull(signerShares);

        if (signerShares.Count < 2)
        {
            throw new ArgumentException("Threshold signing requires at least 2 participating shares.");
        }

        var masterPubKey = BigInteger.Parse(masterPublicKeyHex, System.Globalization.NumberStyles.HexNumber);
        var messageHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(messageText));
        var e = new BigInteger(messageHashBytes, isUnsigned: true, isBigEndian: true) % Q;

        var participatingIndices = signerShares.Select(s => s.ShareIndex).ToList();

        // 1. Each signer generates ephemeral nonce share k_i and ephemeral public commitment R_i = G^k_i mod P
        var nonceShares = new List<BigInteger>();
        BigInteger collectiveR = BigInteger.One;
        BigInteger totalK = BigInteger.Zero;

        foreach (var _ in signerShares)
        {
            var ki = GenerateRandomScalar();
            nonceShares.Add(ki);
            totalK = (totalK + ki) % Q;
            collectiveR = (collectiveR * BigInteger.ModPow(G, ki, P)) % P;
        }

        // 2. Each signer computes partial signature: s_i = (k_i + e * lambda_i * share_i) mod Q
        BigInteger aggregatedS = BigInteger.Zero;

        for (var idx = 0; idx < signerShares.Count; idx++)
        {
            var share = signerShares[idx];
            var shareVal = BigInteger.Parse(share.ShareValueHex, System.Globalization.NumberStyles.HexNumber);
            var lambda = ComputeLagrangeCoefficient(share.ShareIndex, participatingIndices, Q);

            var ki = nonceShares[idx];
            var term = (e * lambda) % Q;
            term = (term * shareVal) % Q;

            var si = (ki + term) % Q;
            aggregatedS = (aggregatedS + si) % Q;
        }

        // 3. Verify threshold signature: G^s == (R * Y^e) mod P
        var lhs = BigInteger.ModPow(G, aggregatedS, P);
        var rhs = (collectiveR * BigInteger.ModPow(masterPubKey, e, P)) % P;
        var isValid = lhs == rhs;

        return new ThresholdSignature(
            SignatureId: $"SIG-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            MessageHashHex: Convert.ToHexString(messageHashBytes),
            EphemeralRHex: collectiveR.ToString("X"),
            AggregatedSHex: aggregatedS.ToString("X"),
            MasterPublicKeyHex: masterPublicKeyHex,
            ParticipatingSignerIndices: participatingIndices,
            IsVerified: isValid,
            SignedAtUtc: DateTime.UtcNow
        );
    }

    public bool VerifySignature(ThresholdSignature signature, string messageText)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var masterPubKey = BigInteger.Parse(signature.MasterPublicKeyHex, System.Globalization.NumberStyles.HexNumber);
        var r = BigInteger.Parse(signature.EphemeralRHex, System.Globalization.NumberStyles.HexNumber);
        var s = BigInteger.Parse(signature.AggregatedSHex, System.Globalization.NumberStyles.HexNumber);

        var messageHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(messageText));
        var e = new BigInteger(messageHashBytes, isUnsigned: true, isBigEndian: true) % Q;

        var lhs = BigInteger.ModPow(G, s, P);
        var rhs = (r * BigInteger.ModPow(masterPubKey, e, P)) % P;

        return lhs == rhs;
    }

    public static BigInteger ComputeLagrangeCoefficient(int j, IReadOnlyList<int> subset, BigInteger modulusQ)
    {
        BigInteger num = BigInteger.One;
        BigInteger den = BigInteger.One;

        foreach (var m in subset)
        {
            if (m == j) continue;

            // num = num * (-m) mod Q
            var minusM = (-m % modulusQ + modulusQ) % modulusQ;
            num = (num * minusM) % modulusQ;

            // den = den * (j - m) mod Q
            var diff = ((j - m) % modulusQ + modulusQ) % modulusQ;
            den = (den * diff) % modulusQ;
        }

        var denInv = ModInverse(den, modulusQ);
        return (num * denInv) % modulusQ;
    }

    private static BigInteger EvaluatePolynomial(List<BigInteger> coefficients, BigInteger x, BigInteger modulusQ)
    {
        BigInteger result = BigInteger.Zero;
        BigInteger xPow = BigInteger.One;

        foreach (var c in coefficients)
        {
            var term = (c * xPow) % modulusQ;
            result = (result + term) % modulusQ;
            xPow = (xPow * x) % modulusQ;
        }

        return result;
    }

    private static BigInteger GenerateRandomScalar()
    {
        var bytes = new byte[32];
        while (true)
        {
            RandomNumberGenerator.Fill(bytes);
            var val = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            if (val > 1 && val < Q)
            {
                return val;
            }
        }
    }

    private static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        var m0 = m;
        BigInteger y = 0, x = 1;

        if (m == 1) return 0;

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

        if (x < 0) x += m0;

        return x;
    }
}
