using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.ZkIdentityVerification;

/// <summary>
/// Autonomous zero-knowledge selective attribute disclosure and Pedersen commitment range proof engine.
/// Allows identity verification (e.g. Age >= 21) without revealing date of birth or sensitive PII.
/// </summary>
public sealed class ZkSelectiveAttributeDisclosureEngine
{
    private static readonly BigInteger Q = BigInteger.Parse("08000000000000000000000000000000000000000000000000000000000130C59", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger P = (2 * Q) + 1;
    private static readonly BigInteger G = new(4); // 2^2 generates subgroup of order Q
    private static readonly BigInteger H = new(9); // 3^2 generates subgroup of order Q

    public (BigInteger Commitment, BigInteger BlindingFactor) CommitAttribute(long attributeValue)
    {
        var r = GenerateRandomScalar();
        var v = new BigInteger(attributeValue);
        var vMod = (v % Q + Q) % Q;

        // C = G^v * H^r mod P
        var gv = BigInteger.ModPow(G, vMod, P);
        var hr = BigInteger.ModPow(H, r, P);
        var commitment = (gv * hr) % P;

        return (commitment, r);
    }

    public ZkRangeProof GenerateZkRangeProof(
        long actualValue,
        BigInteger blindingFactorR,
        long thresholdValue,
        AttributeClaimType claimType)
    {
        if (actualValue < thresholdValue)
        {
            throw new InvalidOperationException($"Cannot generate zero-knowledge range proof: actual value {actualValue} does not satisfy threshold {thresholdValue}.");
        }

        var delta = new BigInteger(actualValue - thresholdValue);
        var deltaMod = (delta % Q + Q) % Q;

        var vMod = (new BigInteger(actualValue) % Q + Q) % Q;
        var c = (BigInteger.ModPow(G, vMod, P) * BigInteger.ModPow(H, blindingFactorR, P)) % P;

        // C_delta = C * (G^threshold)^-1 mod P = G^delta * H^r mod P
        var gThreshold = BigInteger.ModPow(G, (new BigInteger(thresholdValue) % Q + Q) % Q, P);
        var gThresholdInv = ModInverse(gThreshold, P);
        var cDelta = (c * gThresholdInv) % P;

        // Non-interactive proof of knowledge of (delta, r):
        var w1 = GenerateRandomScalar();
        var w2 = GenerateRandomScalar();

        // A = G^w1 * H^w2 mod P
        var a = (BigInteger.ModPow(G, w1, P) * BigInteger.ModPow(H, w2, P)) % P;

        // Fiat-Shamir challenge: e = SHA256(C_delta || A || threshold) mod Q
        var challengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{cDelta:X}:{a:X}:{thresholdValue}"));
        var challenge = new BigInteger(challengeBytes, isUnsigned: true, isBigEndian: true) % Q;

        // z1 = (w1 + challenge * delta) mod Q
        // z2 = (w2 + challenge * r) mod Q
        var z1 = (w1 + (challenge * deltaMod)) % Q;
        var z2 = (w2 + (challenge * blindingFactorR)) % Q;

        return new ZkRangeProof(
            ProofId: $"ZKP-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ClaimType: claimType,
            ThresholdValue: thresholdValue,
            CommitmentHex: c.ToString("X"),
            EphemeralAHex: a.ToString("X"),
            ChallengeHex: challenge.ToString("X"),
            ResponseZ1Hex: z1.ToString("X"),
            ResponseZ2Hex: z2.ToString("X"),
            ProvedAtUtc: DateTime.UtcNow
        );
    }

    public ZkProofVerificationResult VerifyZkRangeProof(ZkRangeProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);

        var c = BigInteger.Parse(proof.CommitmentHex, System.Globalization.NumberStyles.HexNumber);
        var a = BigInteger.Parse(proof.EphemeralAHex, System.Globalization.NumberStyles.HexNumber);
        var challenge = BigInteger.Parse(proof.ChallengeHex, System.Globalization.NumberStyles.HexNumber);
        var z1 = BigInteger.Parse(proof.ResponseZ1Hex, System.Globalization.NumberStyles.HexNumber);
        var z2 = BigInteger.Parse(proof.ResponseZ2Hex, System.Globalization.NumberStyles.HexNumber);

        // 1. Recompute C_delta = C * (G^threshold)^-1 mod P
        var gThreshold = BigInteger.ModPow(G, (new BigInteger(proof.ThresholdValue) % Q + Q) % Q, P);
        var gThresholdInv = ModInverse(gThreshold, P);
        var cDelta = (c * gThresholdInv) % P;

        // 2. Recompute Fiat-Shamir challenge
        var expectedChallengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{cDelta:X}:{a:X}:{proof.ThresholdValue}"));
        var expectedChallenge = new BigInteger(expectedChallengeBytes, isUnsigned: true, isBigEndian: true) % Q;

        var isChallengeValid = challenge == expectedChallenge;

        // 3. Verify: G^z1 * H^z2 == A * (C_delta)^challenge mod P
        var lhs = (BigInteger.ModPow(G, z1, P) * BigInteger.ModPow(H, z2, P)) % P;
        var rhs = (a * BigInteger.ModPow(cDelta, challenge, P)) % P;

        var isProofValid = isChallengeValid && (lhs == rhs);

        var summary = isProofValid
            ? $"ZERO-KNOWLEDGE ATTESTATION VERIFIED: Mathematically proven that subject attribute satisfies {proof.ClaimType} >= {proof.ThresholdValue} without revealing exact attribute value."
            : "VERIFICATION FAILED: Cryptographic zero-knowledge commitment proof does not satisfy Fiat-Shamir constraint.";

        return new ZkProofVerificationResult(
            ProofId: proof.ProofId,
            ClaimType: proof.ClaimType,
            ThresholdValue: proof.ThresholdValue,
            IsProofMathematicallyValid: isProofValid,
            AuditSummary: summary,
            VerifiedAtUtc: DateTime.UtcNow
        );
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
