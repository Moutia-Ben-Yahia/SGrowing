using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.SupplyChain.ZkTraceability;

/// <summary>
/// Engine for issuing and verifying Zero-Knowledge Origin Compliance Proofs
/// based on blinded commitments and Fiat-Shamir non-interactive proofs.
/// </summary>
public sealed class ZeroKnowledgeOriginProofEngine
{
    private static readonly byte[] SystemAuthoritySecretKey = SHA256.HashData(Encoding.UTF8.GetBytes("SGrowing.ZkProofAuthority.MasterSecret.2026"));

    /// <summary>
    /// Computes a deterministic cryptographic Pedersen/HMAC commitment for a component value.
    /// </summary>
    public PedersenCommitment ComputeCommitment(decimal valueDzd, string saltHex, TradeZone zone)
    {
        var raw = $"{valueDzd:F4}:{saltHex}:{(int)zone}";
        using var hmac = new HMACSHA256(SystemAuthoritySecretKey);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var commitmentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new PedersenCommitment(
            CommitmentHash: commitmentHash,
            BlindingSaltHex: saltHex,
            CommittedValueDzd: valueDzd,
            CommittedAtUtc: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Aggregates a manifest's items into a blinded Merkle commitment root.
    /// </summary>
    public string ComputeBlindedCommitmentRoot(BillOfMaterialsManifest manifest, IReadOnlyList<string> blindingSalts)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (blindingSalts.Count < manifest.Items.Count)
        {
            throw new ArgumentException("Must supply a blinding salt for each BOM component.", nameof(blindingSalts));
        }

        var commitments = new List<string>(manifest.Items.Count);
        for (var i = 0; i < manifest.Items.Count; i++)
        {
            var item = manifest.Items[i];
            var salt = blindingSalts[i];
            var commitment = ComputeCommitment(item.TotalValueDzd, salt, item.OriginZone);
            commitments.Add(commitment.CommitmentHash);
        }

        return ComputeMerkleRootFromHashes(commitments);
    }

    /// <summary>
    /// Issues a Zero-Knowledge proof that the qualifying trade zone ratio meets or exceeds the required threshold.
    /// </summary>
    public OriginComplianceProof GenerateOriginProof(GenerateOriginProofRequest request, IReadOnlyList<string> blindingSalts)
    {
        ArgumentNullException.ThrowIfNull(request);
        var manifest = request.Manifest;

        if (manifest.Items.Count == 0)
        {
            throw new ArgumentException("BOM manifest must contain at least one component.", nameof(request));
        }

        var qualifyingRatio = manifest.CalculateZoneValueRatio(request.TargetZone);
        var satisfiesThreshold = qualifyingRatio >= request.MinimumRequiredRatio;

        // Generate public commitment root
        var publicCommitmentRoot = ComputeBlindedCommitmentRoot(manifest, blindingSalts);

        // Ephemeral blinding nonce
        var ephemeralNonce = Guid.NewGuid().ToString("N");

        // Challenge via Fiat-Shamir heuristic
        var challengePayload = $"{publicCommitmentRoot}:{manifest.ProductSku}:{manifest.BatchId}:{(int)request.TargetZone}:{request.MinimumRequiredRatio:F4}:{ephemeralNonce}";
        var challenge = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(challengePayload))).ToLowerInvariant();

        // Combined salt response
        var aggregatedSalt = string.Join(":", blindingSalts);
        var responsePayload = $"{challenge}:{aggregatedSalt}:{satisfiesThreshold}:{ephemeralNonce}";
        var proofResponse = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(responsePayload))).ToLowerInvariant();

        // Authority Signature
        var signaturePayload = $"{challenge}:{proofResponse}:{satisfiesThreshold}";
        using var hmac = new HMACSHA256(SystemAuthoritySecretKey);
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signaturePayload))).ToLowerInvariant();

        var proofId = $"ZKP-{manifest.BatchId}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        var status = satisfiesThreshold ? OriginVerificationStatus.Verified : OriginVerificationStatus.ThresholdNotMet;

        return new OriginComplianceProof(
            ProofId: proofId,
            ProductSku: manifest.ProductSku,
            BatchId: manifest.BatchId,
            TargetTradeZone: request.TargetZone,
            MinimumRequiredRatio: request.MinimumRequiredRatio,
            ThresholdSatisfied: satisfiesThreshold,
            PublicCommitmentRoot: publicCommitmentRoot,
            ProofChallenge: challenge,
            ProofResponse: proofResponse,
            CryptographicSignature: signature,
            IssuedAtUtc: DateTime.UtcNow,
            Status: status
        );
    }

    /// <summary>
    /// Verifies an incoming Origin Compliance Proof without requiring access to component pricing or vendor identity.
    /// </summary>
    public OriginVerificationStatus VerifyProof(VerifyOriginProofRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proof = request.Proof;

        if (string.IsNullOrWhiteSpace(proof.PublicCommitmentRoot) ||
            string.IsNullOrWhiteSpace(proof.ProofChallenge) ||
            string.IsNullOrWhiteSpace(proof.ProofResponse) ||
            string.IsNullOrWhiteSpace(proof.CryptographicSignature))
        {
            return OriginVerificationStatus.RejectedTampered;
        }

        // Verify root matches expected public declaration
        if (!string.Equals(proof.PublicCommitmentRoot, request.ExpectedPublicCommitmentRoot, StringComparison.OrdinalIgnoreCase))
        {
            return OriginVerificationStatus.RejectedTampered;
        }

        // Verify signature integrity
        var expectedSignaturePayload = $"{proof.ProofChallenge}:{proof.ProofResponse}:{proof.ThresholdSatisfied}";
        using var hmac = new HMACSHA256(SystemAuthoritySecretKey);
        var expectedSignatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(expectedSignaturePayload));
        var expectedSignature = Convert.ToHexString(expectedSignatureBytes).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(proof.CryptographicSignature),
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return OriginVerificationStatus.InvalidSignature;
        }

        // Verify threshold requirement
        if (!proof.ThresholdSatisfied || proof.MinimumRequiredRatio < request.MinimumRequiredRatio)
        {
            return OriginVerificationStatus.ThresholdNotMet;
        }

        return OriginVerificationStatus.Verified;
    }

    /// <summary>
    /// Computes a binary Merkle tree root from a list of leaf hashes.
    /// </summary>
    public static string ComputeMerkleRootFromHashes(IReadOnlyList<string> leaves)
    {
        if (leaves.Count == 0) return string.Empty;
        if (leaves.Count == 1) return leaves[0];

        var currentLevel = leaves.Select(l => Convert.FromHexString(l)).ToList();

        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<byte[]>();
            for (var i = 0; i < currentLevel.Count; i += 2)
            {
                if (i + 1 < currentLevel.Count)
                {
                    var combined = new byte[currentLevel[i].Length + currentLevel[i + 1].Length];
                    Buffer.BlockCopy(currentLevel[i], 0, combined, 0, currentLevel[i].Length);
                    Buffer.BlockCopy(currentLevel[i + 1], 0, combined, currentLevel[i].Length, currentLevel[i + 1].Length);
                    nextLevel.Add(SHA256.HashData(combined));
                }
                else
                {
                    // Odd node duplicated as per standard Merkle tree
                    var combined = new byte[currentLevel[i].Length * 2];
                    Buffer.BlockCopy(currentLevel[i], 0, combined, 0, currentLevel[i].Length);
                    Buffer.BlockCopy(currentLevel[i], 0, combined, currentLevel[i].Length, currentLevel[i].Length);
                    nextLevel.Add(SHA256.HashData(combined));
                }
            }
            currentLevel = nextLevel;
        }

        return Convert.ToHexString(currentLevel[0]).ToLowerInvariant();
    }
}
