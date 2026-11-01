using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Agents.Swarm;

/// <summary>
/// Verifiable execution token generated upon successful multi-agent quorum consensus.
/// Encapsulates cryptographic Merkle root of all participating agent votes and HMAC-SHA256 signature.
/// </summary>
public record ConsensusExecutionToken(
    Guid TokenId,
    Guid SessionId,
    Guid TenantId,
    string MerkleRootHash,
    IReadOnlyList<string> LeafHashes,
    double QuorumRatio,
    DateTimeOffset Timestamp,
    DateTimeOffset ExpiresAt,
    string Signature,
    string SignatureAlgorithm = "HMAC-SHA256"
);

/// <summary>
/// Result of verifying an execution token against submitted agent votes and tenant secrets.
/// </summary>
public record TokenVerificationResult(
    bool IsValid,
    string? Reason,
    DateTimeOffset VerifiedAt
);

/// <summary>
/// Service contract for signing and verifying multi-agent consensus decisions.
/// </summary>
public interface ICryptographicQuorumSigner
{
    ConsensusExecutionToken GenerateExecutionToken(
        Guid sessionId,
        Guid tenantId,
        IReadOnlyList<AgentVote> votes,
        double quorumRatio,
        string tenantSecret,
        TimeSpan? validityWindow = null
    );

    TokenVerificationResult VerifyExecutionToken(
        ConsensusExecutionToken token,
        IReadOnlyList<AgentVote> votes,
        string tenantSecret
    );

    string ComputeMerkleRoot(IReadOnlyList<AgentVote> votes, out IReadOnlyList<string> leafHashes);
}

/// <summary>
/// Cryptographic signing and Merkle verification service for multi-agent swarm consensus decisions.
/// Enforces vote immutability, tamper detection, and constant-time HMAC-SHA256 validation.
/// </summary>
public sealed class CryptographicQuorumSigner : ICryptographicQuorumSigner
{
    private static readonly TimeSpan DefaultValidity = TimeSpan.FromHours(24);

    public ConsensusExecutionToken GenerateExecutionToken(
        Guid sessionId,
        Guid tenantId,
        IReadOnlyList<AgentVote> votes,
        double quorumRatio,
        string tenantSecret,
        TimeSpan? validityWindow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantSecret);
        votes ??= [];

        var tokenId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(validityWindow ?? DefaultValidity);

        var merkleRoot = ComputeMerkleRoot(votes, out var leafHashes);
        var canonicalPayload = BuildCanonicalPayload(tokenId, sessionId, tenantId, merkleRoot, quorumRatio, now, expiresAt);
        var signature = ComputeHmacSignature(canonicalPayload, tenantSecret);

        return new ConsensusExecutionToken(
            TokenId: tokenId,
            SessionId: sessionId,
            TenantId: tenantId,
            MerkleRootHash: merkleRoot,
            LeafHashes: leafHashes,
            QuorumRatio: quorumRatio,
            Timestamp: now,
            ExpiresAt: expiresAt,
            Signature: signature
        );
    }

    public TokenVerificationResult VerifyExecutionToken(
        ConsensusExecutionToken token,
        IReadOnlyList<AgentVote> votes,
        string tenantSecret)
    {
        var now = DateTimeOffset.UtcNow;

        if (token == null)
        {
            return new TokenVerificationResult(false, "Execution token is null.", now);
        }

        if (string.IsNullOrWhiteSpace(tenantSecret))
        {
            return new TokenVerificationResult(false, "Tenant signing secret is missing or empty.", now);
        }

        // 1. Expiration check
        if (now > token.ExpiresAt)
        {
            return new TokenVerificationResult(false, $"Token expired at {token.ExpiresAt:O}.", now);
        }

        // 2. Cryptographic signature check (constant-time)
        var canonicalPayload = BuildCanonicalPayload(
            token.TokenId,
            token.SessionId,
            token.TenantId,
            token.MerkleRootHash,
            token.QuorumRatio,
            token.Timestamp,
            token.ExpiresAt
        );

        var expectedSignature = ComputeHmacSignature(canonicalPayload, tenantSecret);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token.Signature),
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return new TokenVerificationResult(false, "Cryptographic signature invalid - tampering detected.", now);
        }

        // 3. Merkle Leaf & Root Integrity Check
        var recomputedRoot = ComputeMerkleRoot(votes ?? [], out var recomputedLeafHashes);
        if (!string.Equals(token.MerkleRootHash, recomputedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new TokenVerificationResult(false, "Merkle root mismatch - votes post-mutation detected.", now);
        }

        // Verify individual leaf hashes match
        if (token.LeafHashes.Count != recomputedLeafHashes.Count)
        {
            return new TokenVerificationResult(false, "Leaf hash count mismatch - votes post-mutation detected.", now);
        }

        for (int i = 0; i < token.LeafHashes.Count; i++)
        {
            if (!string.Equals(token.LeafHashes[i], recomputedLeafHashes[i], StringComparison.OrdinalIgnoreCase))
            {
                return new TokenVerificationResult(false, $"Leaf hash mismatch at index {i} - vote tampering detected.", now);
            }
        }

        return new TokenVerificationResult(true, null, now);
    }

    public string ComputeMerkleRoot(IReadOnlyList<AgentVote> votes, out IReadOnlyList<string> leafHashes)
    {
        if (votes == null || votes.Count == 0)
        {
            var emptyHash = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();
            leafHashes = [];
            return emptyHash;
        }

        // Canonical ordering by AgentRole to ensure determinism across asynchronous agent vote delivery
        var canonicalVotes = votes
            .OrderBy(v => v.AgentRole, StringComparer.Ordinal)
            .ToList();

        var leaves = new List<string>(canonicalVotes.Count);
        foreach (var vote in canonicalVotes)
        {
            var serialized = $"{vote.AgentRole.Trim()}:{vote.Approved}:{(int)Math.Round(vote.ConfidenceScore * 10000)}:{vote.Rationale.Trim()}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();
            leaves.Add(hash);
        }

        leafHashes = leaves;

        // Merkle tree pairwise aggregation
        var currentLevel = leaves.Select(Convert.FromHexString).ToList();

        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<byte[]>();
            for (int i = 0; i < currentLevel.Count; i += 2)
            {
                var left = currentLevel[i];
                var right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : left; // Duplicate right node if odd count (RFC 6962)

                var combined = new byte[left.Length + right.Length];
                Buffer.BlockCopy(left, 0, combined, 0, left.Length);
                Buffer.BlockCopy(right, 0, combined, left.Length, right.Length);

                nextLevel.Add(SHA256.HashData(combined));
            }

            currentLevel = nextLevel;
        }

        return Convert.ToHexString(currentLevel[0]).ToLowerInvariant();
    }

    private static string BuildCanonicalPayload(
        Guid tokenId,
        Guid sessionId,
        Guid tenantId,
        string merkleRoot,
        double quorumRatio,
        DateTimeOffset timestamp,
        DateTimeOffset expiresAt)
    {
        return FormattableString.Invariant($"{tokenId}:{sessionId}:{tenantId}:{merkleRoot}:{quorumRatio:F4}:{timestamp:O}:{expiresAt:O}");
    }

    private static string ComputeHmacSignature(string message, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
