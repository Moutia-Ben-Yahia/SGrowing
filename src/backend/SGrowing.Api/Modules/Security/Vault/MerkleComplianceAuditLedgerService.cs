using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Security.Vault;

public enum MerkleDirection
{
    Left = 0,
    Right = 1
}

public record MerkleAuditProofStep(
    string SiblingHash,
    MerkleDirection Direction
);

public record ComplianceAuditBlock(
    long Index,
    Guid EventId,
    Guid TenantId,
    string Actor,
    string Action,
    string Resource,
    string MetadataJson,
    DateTime TimestampUtc,
    string PreviousBlockHash,
    string BlockHash,
    DateTime RecordedAtUtc
);

public record MerkleEpochProof(
    string EpochId,
    Guid TenantId,
    DateTime EpochStartUtc,
    DateTime EpochEndUtc,
    int BlockCount,
    string MerkleRoot,
    List<string> LeafHashes,
    List<List<string>> TreeLevels
);

public record MerkleAuditInclusionProof(
    Guid EventId,
    string LeafHash,
    int LeafIndex,
    int TotalLeaves,
    string MerkleRoot,
    List<MerkleAuditProofStep> ProofPath
);

public record LedgerIntegrityResult(
    bool IsValid,
    Guid TenantId,
    int TotalBlocksVerified,
    string GenesisHash,
    string LatestBlockHash,
    string CurrentMerkleRoot,
    List<string> Violations,
    DateTime VerifiedAtUtc
);

public interface IMerkleComplianceAuditLedgerService
{
    Task<ComplianceAuditBlock> AppendAuditBlockAsync(
        Guid tenantId,
        string actor,
        string action,
        string resource,
        string? metadataJson = null,
        DateTime? timestampUtc = null);

    Task<LedgerIntegrityResult> VerifyLedgerIntegrityAsync(Guid tenantId);

    Task<MerkleEpochProof> GenerateEpochMerkleRootAsync(
        Guid tenantId,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null);

    Task<MerkleAuditInclusionProof?> GenerateInclusionProofAsync(Guid tenantId, Guid eventId);

    bool VerifyInclusionProof(string leafHash, List<MerkleAuditProofStep> proofPath, string expectedMerkleRoot);

    Task<IReadOnlyList<ComplianceAuditBlock>> GetBlocksAsync(Guid tenantId);

    Task<ComplianceAuditBlock?> GetBlockByEventIdAsync(Guid tenantId, Guid eventId);

    Task<bool> TamperBlockForTestingAsync(
        Guid tenantId,
        Guid eventId,
        string? tamperedActor = null,
        string? tamperedAction = null,
        string? tamperedResource = null);
}

public class MerkleComplianceAuditLedgerService : IMerkleComplianceAuditLedgerService
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly ConcurrentDictionary<Guid, List<ComplianceAuditBlock>> _tenantLedgers = new();
    private readonly ConcurrentDictionary<Guid, object> _tenantLocks = new();

    public static string ComputeBlockHash(
        string previousBlockHash,
        Guid eventId,
        Guid tenantId,
        string actor,
        string action,
        string resource,
        DateTime timestampUtc)
    {
        // Hi = SHA256(Hi-1 || EventId || TenantId || Actor || Action || Resource || TimestampUtc)
        var input = $"{previousBlockHash}|{eventId:D}|{tenantId:D}|{actor.Trim()}|{action.Trim()}|{resource.Trim()}|{timestampUtc:O}";
        return ComputeSha256Hex(input);
    }

    public static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string ComputeSha256Hex(byte[] input)
    {
        var hashBytes = SHA256.HashData(input);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public Task<ComplianceAuditBlock> AppendAuditBlockAsync(
        Guid tenantId,
        string actor,
        string action,
        string resource,
        string? metadataJson = null,
        DateTime? timestampUtc = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("Actor must not be empty.", nameof(actor));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action must not be empty.", nameof(action));
        if (string.IsNullOrWhiteSpace(resource))
            throw new ArgumentException("Resource must not be empty.", nameof(resource));

        var tenantLock = _tenantLocks.GetOrAdd(tenantId, _ => new object());
        var eventTime = timestampUtc ?? DateTime.UtcNow;
        var eventId = Guid.NewGuid();
        var normalizedMetadata = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson.Trim();

        lock (tenantLock)
        {
            var ledger = _tenantLedgers.GetOrAdd(tenantId, _ => []);
            var previousHash = ledger.Count == 0 ? GenesisHash : ledger[^1].BlockHash;
            var index = ledger.Count;

            var blockHash = ComputeBlockHash(
                previousHash,
                eventId,
                tenantId,
                actor,
                action,
                resource,
                eventTime);

            var block = new ComplianceAuditBlock(
                Index: index,
                EventId: eventId,
                TenantId: tenantId,
                Actor: actor.Trim(),
                Action: action.Trim(),
                Resource: resource.Trim(),
                MetadataJson: normalizedMetadata,
                TimestampUtc: eventTime,
                PreviousBlockHash: previousHash,
                BlockHash: blockHash,
                RecordedAtUtc: DateTime.UtcNow
            );

            ledger.Add(block);
            return Task.FromResult(block);
        }
    }

    public Task<IReadOnlyList<ComplianceAuditBlock>> GetBlocksAsync(Guid tenantId)
    {
        if (_tenantLedgers.TryGetValue(tenantId, out var ledger))
        {
            lock (_tenantLocks.GetOrAdd(tenantId, _ => new object()))
            {
                return Task.FromResult<IReadOnlyList<ComplianceAuditBlock>>(ledger.ToList());
            }
        }

        return Task.FromResult<IReadOnlyList<ComplianceAuditBlock>>([]);
    }

    public Task<ComplianceAuditBlock?> GetBlockByEventIdAsync(Guid tenantId, Guid eventId)
    {
        if (_tenantLedgers.TryGetValue(tenantId, out var ledger))
        {
            lock (_tenantLocks.GetOrAdd(tenantId, _ => new object()))
            {
                var block = ledger.FirstOrDefault(b => b.EventId == eventId);
                return Task.FromResult(block);
            }
        }

        return Task.FromResult<ComplianceAuditBlock?>(null);
    }

    public Task<LedgerIntegrityResult> VerifyLedgerIntegrityAsync(Guid tenantId)
    {
        var violations = new List<string>();

        if (!_tenantLedgers.TryGetValue(tenantId, out var ledger) || ledger.Count == 0)
        {
            return Task.FromResult(new LedgerIntegrityResult(
                IsValid: true,
                TenantId: tenantId,
                TotalBlocksVerified: 0,
                GenesisHash: GenesisHash,
                LatestBlockHash: GenesisHash,
                CurrentMerkleRoot: GenesisHash,
                Violations: [],
                VerifiedAtUtc: DateTime.UtcNow
            ));
        }

        lock (_tenantLocks.GetOrAdd(tenantId, _ => new object()))
        {
            var expectedPreviousHash = GenesisHash;

            for (int i = 0; i < ledger.Count; i++)
            {
                var block = ledger[i];

                // 1. Verify index sequence
                if (block.Index != i)
                {
                    violations.Add($"Index sequence mismatch at position {i}: expected index {i}, but block has index {block.Index}.");
                }

                // 2. Verify previous block hash linkage
                if (!string.Equals(block.PreviousBlockHash, expectedPreviousHash, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"Broken block chain at index {block.Index} (EventId {block.EventId}): PreviousBlockHash '{block.PreviousBlockHash}' does not match expected previous hash '{expectedPreviousHash}'.");
                }

                // 3. Cryptographically recompute SHA-256 block hash
                var recomputedHash = ComputeBlockHash(
                    block.PreviousBlockHash,
                    block.EventId,
                    block.TenantId,
                    block.Actor,
                    block.Action,
                    block.Resource,
                    block.TimestampUtc);

                if (!string.Equals(block.BlockHash, recomputedHash, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"Tampered block detected at index {block.Index} (EventId {block.EventId}): recomputed hash '{recomputedHash}' does not match recorded block hash '{block.BlockHash}'.");
                }

                expectedPreviousHash = block.BlockHash;
            }

            var tree = BuildMerkleTree(ledger.Select(b => b.BlockHash).ToList());
            var latestBlockHash = ledger[^1].BlockHash;

            return Task.FromResult(new LedgerIntegrityResult(
                IsValid: violations.Count == 0,
                TenantId: tenantId,
                TotalBlocksVerified: ledger.Count,
                GenesisHash: GenesisHash,
                LatestBlockHash: latestBlockHash,
                CurrentMerkleRoot: tree.RootHash,
                Violations: violations,
                VerifiedAtUtc: DateTime.UtcNow
            ));
        }
    }

    public Task<MerkleEpochProof> GenerateEpochMerkleRootAsync(
        Guid tenantId,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null)
    {
        var start = startTimeUtc ?? DateTime.MinValue;
        var end = endTimeUtc ?? DateTime.MaxValue;

        var blocks = new List<ComplianceAuditBlock>();
        if (_tenantLedgers.TryGetValue(tenantId, out var ledger))
        {
            lock (_tenantLocks.GetOrAdd(tenantId, _ => new object()))
            {
                blocks = ledger
                    .Where(b => b.TimestampUtc >= start && b.TimestampUtc <= end)
                    .OrderBy(b => b.Index)
                    .ToList();
            }
        }

        var leafHashes = blocks.Select(b => b.BlockHash).ToList();
        var tree = BuildMerkleTree(leafHashes);
        var epochId = $"epoch_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..28];

        var proof = new MerkleEpochProof(
            EpochId: epochId,
            TenantId: tenantId,
            EpochStartUtc: start == DateTime.MinValue ? (blocks.Count > 0 ? blocks[0].TimestampUtc : DateTime.UtcNow) : start,
            EpochEndUtc: end == DateTime.MaxValue ? (blocks.Count > 0 ? blocks[^1].TimestampUtc : DateTime.UtcNow) : end,
            BlockCount: blocks.Count,
            MerkleRoot: tree.RootHash,
            LeafHashes: leafHashes,
            TreeLevels: tree.Levels
        );

        return Task.FromResult(proof);
    }

    public Task<MerkleAuditInclusionProof?> GenerateInclusionProofAsync(Guid tenantId, Guid eventId)
    {
        if (!_tenantLedgers.TryGetValue(tenantId, out var ledger))
            return Task.FromResult<MerkleAuditInclusionProof?>(null);

        lock (_tenantLocks.GetOrAdd(tenantId, _ => new object()))
        {
            int leafIndex = ledger.FindIndex(b => b.EventId == eventId);
            if (leafIndex < 0)
                return Task.FromResult<MerkleAuditInclusionProof?>(null);

            var leafHashes = ledger.Select(b => b.BlockHash).ToList();
            var tree = BuildMerkleTree(leafHashes);
            var proofPath = new List<MerkleAuditProofStep>();

            int currentIndex = leafIndex;
            for (int l = 0; l < tree.Levels.Count - 1; l++)
            {
                var currentLevel = tree.Levels[l];
                bool isRightNode = (currentIndex % 2) == 1;
                int siblingIndex = isRightNode ? currentIndex - 1 : currentIndex + 1;

                if (siblingIndex < currentLevel.Count)
                {
                    proofPath.Add(new MerkleAuditProofStep(
                        SiblingHash: currentLevel[siblingIndex],
                        Direction: isRightNode ? MerkleDirection.Left : MerkleDirection.Right
                    ));
                }

                currentIndex /= 2;
            }

            var proof = new MerkleAuditInclusionProof(
                EventId: eventId,
                LeafHash: ledger[leafIndex].BlockHash,
                LeafIndex: leafIndex,
                TotalLeaves: leafHashes.Count,
                MerkleRoot: tree.RootHash,
                ProofPath: proofPath
            );

            return Task.FromResult<MerkleAuditInclusionProof?>(proof);
        }
    }

    public bool VerifyInclusionProof(string leafHash, List<MerkleAuditProofStep> proofPath, string expectedMerkleRoot)
    {
        if (string.IsNullOrWhiteSpace(leafHash) || string.IsNullOrWhiteSpace(expectedMerkleRoot))
            return false;

        var currentHash = leafHash.ToLowerInvariant();

        foreach (var step in proofPath)
        {
            var sibling = step.SiblingHash.ToLowerInvariant();
            currentHash = step.Direction == MerkleDirection.Left
                ? HashPair(sibling, currentHash)
                : HashPair(currentHash, sibling);
        }

        return string.Equals(currentHash, expectedMerkleRoot, StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> TamperBlockForTestingAsync(
        Guid tenantId,
        Guid eventId,
        string? tamperedActor = null,
        string? tamperedAction = null,
        string? tamperedResource = null)
    {
        if (!_tenantLedgers.TryGetValue(tenantId, out var ledger))
            return Task.FromResult(false);

        lock (_tenantLocks.GetOrAdd(tenantId, _ => new object()))
        {
            int index = ledger.FindIndex(b => b.EventId == eventId);
            if (index < 0) return Task.FromResult(false);

            var original = ledger[index];
            var tampered = original with
            {
                Actor = tamperedActor ?? original.Actor,
                Action = tamperedAction ?? original.Action,
                Resource = tamperedResource ?? original.Resource
            };

            ledger[index] = tampered;
            return Task.FromResult(true);
        }
    }

    private static (string RootHash, List<List<string>> Levels) BuildMerkleTree(IReadOnlyList<string> leafHashes)
    {
        if (leafHashes == null || leafHashes.Count == 0)
        {
            return (GenesisHash, [[]]);
        }

        var normalizedLeaves = leafHashes.Select(h => h.ToLowerInvariant()).ToList();
        var levels = new List<List<string>> { new(normalizedLeaves) };

        var currentLevel = normalizedLeaves;
        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<string>();
            for (int i = 0; i < currentLevel.Count; i += 2)
            {
                var left = currentLevel[i];
                var right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : left; // Pair with self if odd count
                nextLevel.Add(HashPair(left, right));
            }

            levels.Add(nextLevel);
            currentLevel = nextLevel;
        }

        return (currentLevel[0], levels);
    }

    private static string HashPair(string left, string right)
    {
        return ComputeSha256Hex($"{left}:{right}");
    }
}
