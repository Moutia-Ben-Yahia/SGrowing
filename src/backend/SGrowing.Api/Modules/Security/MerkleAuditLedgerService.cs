using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Security;

public enum MerkleProofDirection
{
    Left = 0,
    Right = 1
}

public record MerkleProofStep(
    string SiblingHash,
    MerkleProofDirection Direction
);

public record MerkleAuditEvent(
    Guid Id,
    Guid TenantId,
    Guid ActorId,
    string ActorEmail,
    string Action,
    string Resource,
    DateTime TimestampUtc,
    string MetadataJson,
    string PayloadHash,
    string LeafHash,
    Guid? EpochId = null,
    long? EpochNumber = null,
    bool IsSealed = false
);

public record MerkleTreeResult(
    string RootHash,
    int LeafCount,
    int TotalNodeCount,
    List<List<string>> Levels
);

public record MerkleInclusionProof(
    Guid EventId,
    string LeafHash,
    int LeafIndex,
    int TotalLeaves,
    string MerkleRoot,
    Guid? EpochId,
    long? EpochNumber,
    bool IsSealed,
    List<MerkleProofStep> ProofPath
);

public record MerkleEpochRecord(
    Guid Id,
    Guid TenantId,
    long EpochNumber,
    string EpochLabel,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    DateTime SealedAtUtc,
    string SealedBy,
    int EventCount,
    string MerkleRoot,
    string EpochSignature,
    string PreviousEpochChainedHash,
    string ChainedHash,
    List<Guid> EventIds,
    bool IsSealed
);

public record SimulateTamperResult(
    Guid EventId,
    string OriginalPayloadHash,
    string TamperedPayloadHash,
    string OriginalLeafHash,
    string TamperedLeafHash,
    string OriginalMerkleRoot,
    string TamperedMerkleRoot,
    bool RootMismatchDetected,
    bool ProofVerificationFailed,
    bool IntegrityBroken,
    string TamperAuditVerdict,
    List<MerkleProofStep> OriginalProofPath
);

public record ComplianceManifestEventEntry(
    Guid EventId,
    int SequenceIndex,
    DateTime TimestampUtc,
    string ActorEmail,
    string Action,
    string Resource,
    string PayloadHash,
    string LeafHash
);

public record ComplianceVerificationGuide(
    string Algorithm,
    string HmacAlgorithm,
    string LeafHashFormula,
    string TreePairingFormula,
    string ChainingFormula,
    string StandardsCompliance,
    string VerificationInstructions
);

public record ComplianceManifestDto(
    string ManifestVersion,
    string ComplianceStandard,
    DateTime GeneratedAtUtc,
    Guid TenantId,
    Guid EpochId,
    long EpochNumber,
    string EpochLabel,
    DateTime EpochStartTimeUtc,
    DateTime EpochEndTimeUtc,
    DateTime EpochSealedAtUtc,
    string SealedBy,
    int EventCount,
    string MerkleRoot,
    string EpochSignature,
    string ChainedHash,
    string PreviousChainedHash,
    List<ComplianceManifestEventEntry> Events,
    ComplianceVerificationGuide VerificationGuide
);

public interface IMerkleAuditLedgerService
{
    string ComputePayloadHash(string? metadataJson);
    string ComputeLeafHash(Guid tenantId, DateTime timestampUtc, string actorEmail, string action, string resource, string payloadHash);
    MerkleTreeResult BuildMerkleTree(IReadOnlyList<string> leafHashes);
    bool VerifyInclusionProof(string leafHash, List<MerkleProofStep> proofPath, string expectedRootHash);

    Task<MerkleAuditEvent> RecordAuditEventAsync(
        Guid tenantId,
        Guid actorId,
        string actorEmail,
        string action,
        string resource,
        string? metadataJson = null,
        DateTime? timestampUtc = null);

    Task<List<MerkleAuditEvent>> GetEventsAsync(
        Guid tenantId,
        string? action = null,
        string? resource = null,
        bool? isSealed = null,
        int limit = 50,
        int offset = 0);

    Task<MerkleAuditEvent?> GetEventByIdAsync(Guid eventId);

    Task<MerkleInclusionProof> GetInclusionProofAsync(Guid eventId);

    Task<MerkleEpochRecord> SealCurrentEpochAsync(Guid tenantId, string sealedBy = "Automated Security Scheduler");

    Task<List<MerkleEpochRecord>> GetEpochsAsync(Guid tenantId);

    Task<MerkleEpochRecord?> GetEpochByIdAsync(Guid epochId);

    Task<ComplianceManifestDto> GenerateComplianceManifestAsync(Guid tenantId, Guid? epochId = null);

    Task<SimulateTamperResult> SimulateTamperAsync(Guid eventId, string? tamperedMetadata = null);
}

public class MerkleAuditLedgerService : IMerkleAuditLedgerService
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Lazy<MerkleAuditLedgerService> _lazyInstance = new(() =>
    {
        var svc = new MerkleAuditLedgerService("SGrowing_Secret_Epoch_HMAC_Signing_Key_2026_SecureKey!");
        svc.SeedRealisticAuditLedger(DefaultTenantId);
        return svc;
    });

    public static IMerkleAuditLedgerService DefaultInstance => _lazyInstance.Value;

    private readonly string _hmacKey;
    private readonly object _syncLock = new();
    private readonly List<MerkleAuditEvent> _events = new();
    private readonly List<MerkleEpochRecord> _epochs = new();

    public MerkleAuditLedgerService(string hmacKey = "SGrowing_Secret_Epoch_HMAC_Signing_Key_2026_SecureKey!")
    {
        _hmacKey = hmacKey;
    }

    public string ComputePayloadHash(string? metadataJson)
    {
        string normalized = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson.Trim();
        return ComputeSha256Hex(normalized);
    }

    public string ComputeLeafHash(
        Guid tenantId,
        DateTime timestampUtc,
        string actorEmail,
        string action,
        string resource,
        string payloadHash)
    {
        string raw = $"{tenantId}|{timestampUtc:O}|{actorEmail}|{action}|{resource}|{payloadHash}";
        return ComputeSha256Hex(raw);
    }

    public MerkleTreeResult BuildMerkleTree(IReadOnlyList<string> leafHashes)
    {
        if (leafHashes == null || leafHashes.Count == 0)
        {
            return new MerkleTreeResult(
                GenesisHash,
                0,
                0,
                new List<List<string>>());
        }

        if (leafHashes.Count == 1)
        {
            return new MerkleTreeResult(
                leafHashes[0],
                1,
                1,
                new List<List<string>> { new() { leafHashes[0] } });
        }

        var levels = new List<List<string>>();
        var currentLevel = new List<string>(leafHashes);
        levels.Add(new List<string>(currentLevel));

        int totalNodeCount = currentLevel.Count;

        while (currentLevel.Count > 1)
        {
            // If odd number of nodes in this level, duplicate the last node to form complete pairs
            if (currentLevel.Count % 2 != 0)
            {
                currentLevel.Add(currentLevel[^1]);
            }

            var nextLevel = new List<string>(currentLevel.Count / 2);
            for (int i = 0; i < currentLevel.Count; i += 2)
            {
                string combined = currentLevel[i] + currentLevel[i + 1];
                string parentHash = ComputeSha256Hex(combined);
                nextLevel.Add(parentHash);
            }

            totalNodeCount += nextLevel.Count;
            levels.Add(new List<string>(nextLevel));
            currentLevel = nextLevel;
        }

        string rootHash = levels[^1][0];
        return new MerkleTreeResult(rootHash, leafHashes.Count, totalNodeCount, levels);
    }

    public bool VerifyInclusionProof(string leafHash, List<MerkleProofStep> proofPath, string expectedRootHash)
    {
        if (string.IsNullOrWhiteSpace(leafHash) || string.IsNullOrWhiteSpace(expectedRootHash))
            return false;

        if (proofPath == null || proofPath.Count == 0)
        {
            return string.Equals(leafHash, expectedRootHash, StringComparison.OrdinalIgnoreCase);
        }

        string currentHash = leafHash;
        foreach (var step in proofPath)
        {
            if (string.IsNullOrWhiteSpace(step.SiblingHash))
                return false;

            string combined = step.Direction == MerkleProofDirection.Left
                ? step.SiblingHash + currentHash
                : currentHash + step.SiblingHash;

            currentHash = ComputeSha256Hex(combined);
        }

        return string.Equals(currentHash, expectedRootHash, StringComparison.OrdinalIgnoreCase);
    }

    public Task<MerkleAuditEvent> RecordAuditEventAsync(
        Guid tenantId,
        Guid actorId,
        string actorEmail,
        string action,
        string resource,
        string? metadataJson = null,
        DateTime? timestampUtc = null)
    {
        var timestamp = timestampUtc ?? DateTime.UtcNow;
        var metadata = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        var payloadHash = ComputePayloadHash(metadata);
        var leafHash = ComputeLeafHash(tenantId, timestamp, actorEmail, action, resource, payloadHash);

        var auditEvent = new MerkleAuditEvent(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            ActorId: actorId,
            ActorEmail: actorEmail,
            Action: action,
            Resource: resource,
            TimestampUtc: timestamp,
            MetadataJson: metadata,
            PayloadHash: payloadHash,
            LeafHash: leafHash,
            EpochId: null,
            EpochNumber: null,
            IsSealed: false
        );

        lock (_syncLock)
        {
            _events.Add(auditEvent);
        }

        return Task.FromResult(auditEvent);
    }

    public Task<List<MerkleAuditEvent>> GetEventsAsync(
        Guid tenantId,
        string? action = null,
        string? resource = null,
        bool? isSealed = null,
        int limit = 50,
        int offset = 0)
    {
        lock (_syncLock)
        {
            var query = _events.AsEnumerable();

            if (tenantId != Guid.Empty)
            {
                query = query.Where(e => e.TenantId == tenantId);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(e => string.Equals(e.Action, action, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(resource))
            {
                query = query.Where(e => string.Equals(e.Resource, resource, StringComparison.OrdinalIgnoreCase));
            }

            if (isSealed.HasValue)
            {
                query = query.Where(e => e.IsSealed == isSealed.Value);
            }

            var result = query
                .OrderByDescending(e => e.TimestampUtc)
                .Skip(offset)
                .Take(limit)
                .ToList();

            return Task.FromResult(result);
        }
    }

    public Task<MerkleAuditEvent?> GetEventByIdAsync(Guid eventId)
    {
        lock (_syncLock)
        {
            var ev = _events.FirstOrDefault(e => e.Id == eventId);
            return Task.FromResult(ev);
        }
    }

    public Task<MerkleInclusionProof> GetInclusionProofAsync(Guid eventId)
    {
        lock (_syncLock)
        {
            var targetEvent = _events.FirstOrDefault(e => e.Id == eventId);
            if (targetEvent == null)
            {
                throw new KeyNotFoundException($"Audit event with Id '{eventId}' was not found.");
            }

            List<MerkleAuditEvent> batch;
            if (targetEvent.IsSealed && targetEvent.EpochId.HasValue)
            {
                batch = _events
                    .Where(e => e.EpochId == targetEvent.EpochId.Value)
                    .OrderBy(e => e.TimestampUtc)
                    .ToList();
            }
            else
            {
                // Unsealed batch for this tenant
                batch = _events
                    .Where(e => e.TenantId == targetEvent.TenantId && !e.IsSealed)
                    .OrderBy(e => e.TimestampUtc)
                    .ToList();
            }

            int leafIndex = batch.FindIndex(e => e.Id == targetEvent.Id);
            if (leafIndex < 0)
            {
                throw new InvalidOperationException("Could not locate target event in the Merkle batch.");
            }

            var leafHashes = batch.Select(e => e.LeafHash).ToList();
            var tree = BuildMerkleTree(leafHashes);
            var proofPath = GenerateProofPath(tree.Levels, leafIndex);

            var proof = new MerkleInclusionProof(
                EventId: targetEvent.Id,
                LeafHash: targetEvent.LeafHash,
                LeafIndex: leafIndex,
                TotalLeaves: leafHashes.Count,
                MerkleRoot: tree.RootHash,
                EpochId: targetEvent.EpochId,
                EpochNumber: targetEvent.EpochNumber,
                IsSealed: targetEvent.IsSealed,
                ProofPath: proofPath
            );

            return Task.FromResult(proof);
        }
    }

    public Task<MerkleEpochRecord> SealCurrentEpochAsync(Guid tenantId, string sealedBy = "Automated Security Scheduler")
    {
        lock (_syncLock)
        {
            var effectiveTenantId = tenantId == Guid.Empty ? DefaultTenantId : tenantId;

            var unsealed = _events
                .Where(e => (effectiveTenantId == Guid.Empty || e.TenantId == effectiveTenantId) && !e.IsSealed)
                .OrderBy(e => e.TimestampUtc)
                .ToList();

            if (unsealed.Count == 0)
            {
                throw new InvalidOperationException("No unsealed events available to seal in current epoch.");
            }

            var previousEpoch = _epochs
                .Where(ep => ep.TenantId == effectiveTenantId)
                .OrderByDescending(ep => ep.EpochNumber)
                .FirstOrDefault();

            long nextEpochNumber = (previousEpoch?.EpochNumber ?? 0) + 1;
            string previousChainedHash = previousEpoch?.ChainedHash ?? GenesisHash;

            var leafHashes = unsealed.Select(e => e.LeafHash).ToList();
            var tree = BuildMerkleTree(leafHashes);

            string epochSignature = ComputeHmacSha256Hex(_hmacKey, tree.RootHash);
            string chainedHash = ComputeSha256Hex(tree.RootHash + previousChainedHash);

            var epochId = Guid.NewGuid();
            var epoch = new MerkleEpochRecord(
                Id: epochId,
                TenantId: effectiveTenantId,
                EpochNumber: nextEpochNumber,
                EpochLabel: $"Epoch #{nextEpochNumber}",
                StartTimeUtc: unsealed.First().TimestampUtc,
                EndTimeUtc: unsealed.Last().TimestampUtc,
                SealedAtUtc: DateTime.UtcNow,
                SealedBy: sealedBy,
                EventCount: unsealed.Count,
                MerkleRoot: tree.RootHash,
                EpochSignature: epochSignature,
                PreviousEpochChainedHash: previousChainedHash,
                ChainedHash: chainedHash,
                EventIds: unsealed.Select(e => e.Id).ToList(),
                IsSealed: true
            );

            // Mutate events to sealed state
            for (int i = 0; i < _events.Count; i++)
            {
                var ev = _events[i];
                if (unsealed.Any(u => u.Id == ev.Id))
                {
                    _events[i] = ev with
                    {
                        IsSealed = true,
                        EpochId = epochId,
                        EpochNumber = nextEpochNumber
                    };
                }
            }

            _epochs.Add(epoch);
            return Task.FromResult(epoch);
        }
    }

    public Task<List<MerkleEpochRecord>> GetEpochsAsync(Guid tenantId)
    {
        lock (_syncLock)
        {
            var effectiveTenantId = tenantId == Guid.Empty ? DefaultTenantId : tenantId;
            var list = _epochs
                .Where(ep => effectiveTenantId == Guid.Empty || ep.TenantId == effectiveTenantId)
                .OrderByDescending(ep => ep.EpochNumber)
                .ToList();

            return Task.FromResult(list);
        }
    }

    public Task<MerkleEpochRecord?> GetEpochByIdAsync(Guid epochId)
    {
        lock (_syncLock)
        {
            var ep = _epochs.FirstOrDefault(e => e.Id == epochId);
            return Task.FromResult(ep);
        }
    }

    public Task<ComplianceManifestDto> GenerateComplianceManifestAsync(Guid tenantId, Guid? epochId = null)
    {
        lock (_syncLock)
        {
            var effectiveTenantId = tenantId == Guid.Empty ? DefaultTenantId : tenantId;

            MerkleEpochRecord? epoch;
            if (epochId.HasValue && epochId.Value != Guid.Empty)
            {
                epoch = _epochs.FirstOrDefault(e => e.Id == epochId.Value);
            }
            else
            {
                epoch = _epochs
                    .Where(e => effectiveTenantId == Guid.Empty || e.TenantId == effectiveTenantId)
                    .OrderByDescending(e => e.EpochNumber)
                    .FirstOrDefault();
            }

            if (epoch == null)
            {
                throw new KeyNotFoundException("No sealed epoch found to generate compliance manifest.");
            }

            var epochEvents = _events
                .Where(e => e.EpochId == epoch.Id)
                .OrderBy(e => e.TimestampUtc)
                .Select((e, idx) => new ComplianceManifestEventEntry(
                    EventId: e.Id,
                    SequenceIndex: idx,
                    TimestampUtc: e.TimestampUtc,
                    ActorEmail: e.ActorEmail,
                    Action: e.Action,
                    Resource: e.Resource,
                    PayloadHash: e.PayloadHash,
                    LeafHash: e.LeafHash
                ))
                .ToList();

            var guide = new ComplianceVerificationGuide(
                Algorithm: "SHA-256 binary Merkle tree with odd-leaf duplicate promotion",
                HmacAlgorithm: "HMAC-SHA256 epoch seal signature",
                LeafHashFormula: "SHA256(TenantId|TimestampUtc:O|ActorEmail|Action|Resource|PayloadHash)",
                TreePairingFormula: "SHA256(LeftHash + RightHash)",
                ChainingFormula: "SHA256(CurrentEpochMerkleRoot + PreviousEpochChainedHash)",
                StandardsCompliance: "SOC 2 Type II (Trust Services Criteria CC6.1, CC6.6, CC6.8), GDPR Articles 30 & 32 (Immutable audit records), ISO/IEC 27001:2022 A.8.15",
                VerificationInstructions: "1. For each event entry, verify LeafHash == SHA256(TenantId|TimestampUtc:O|ActorEmail|Action|Resource|SHA256(MetadataJson)). 2. Construct Merkle tree from all LeafHashes in SequenceIndex order. 3. Confirm calculated Merkle Root matches epoch MerkleRoot. 4. Verify ChainedHash matches SHA256(MerkleRoot + PreviousEpochChainedHash)."
            );

            var manifest = new ComplianceManifestDto(
                ManifestVersion: "2026.1-cryptographic-merkle-audit",
                ComplianceStandard: "SOC2-GDPR-Audit-Ledger-v1",
                GeneratedAtUtc: DateTime.UtcNow,
                TenantId: epoch.TenantId,
                EpochId: epoch.Id,
                EpochNumber: epoch.EpochNumber,
                EpochLabel: epoch.EpochLabel,
                EpochStartTimeUtc: epoch.StartTimeUtc,
                EpochEndTimeUtc: epoch.EndTimeUtc,
                EpochSealedAtUtc: epoch.SealedAtUtc,
                SealedBy: epoch.SealedBy,
                EventCount: epochEvents.Count,
                MerkleRoot: epoch.MerkleRoot,
                EpochSignature: epoch.EpochSignature,
                ChainedHash: epoch.ChainedHash,
                PreviousChainedHash: epoch.PreviousEpochChainedHash,
                Events: epochEvents,
                VerificationGuide: guide
            );

            return Task.FromResult(manifest);
        }
    }

    public Task<SimulateTamperResult> SimulateTamperAsync(Guid eventId, string? tamperedMetadata = null)
    {
        lock (_syncLock)
        {
            var targetEvent = _events.FirstOrDefault(e => e.Id == eventId);
            if (targetEvent == null)
            {
                throw new KeyNotFoundException($"Event with id '{eventId}' was not found.");
            }

            string forgedPayload = string.IsNullOrWhiteSpace(tamperedMetadata)
                ? "{\"adversary\":\"forged_attacker_payload\",\"action\":\"AdversarialPrivilegeEscalation\",\"target\":\"root_admin\"}"
                : tamperedMetadata;

            string originalPayloadHash = targetEvent.PayloadHash;
            string tamperedPayloadHash = ComputePayloadHash(forgedPayload);

            string originalLeafHash = targetEvent.LeafHash;
            string tamperedLeafHash = ComputeLeafHash(
                targetEvent.TenantId,
                targetEvent.TimestampUtc,
                targetEvent.ActorEmail,
                targetEvent.Action,
                targetEvent.Resource,
                tamperedPayloadHash);

            // Retrieve batch for this event
            List<MerkleAuditEvent> batch;
            if (targetEvent.IsSealed && targetEvent.EpochId.HasValue)
            {
                batch = _events
                    .Where(e => e.EpochId == targetEvent.EpochId.Value)
                    .OrderBy(e => e.TimestampUtc)
                    .ToList();
            }
            else
            {
                batch = _events
                    .Where(e => e.TenantId == targetEvent.TenantId && !e.IsSealed)
                    .OrderBy(e => e.TimestampUtc)
                    .ToList();
            }

            int targetIndex = batch.FindIndex(e => e.Id == targetEvent.Id);
            var originalLeaves = batch.Select(e => e.LeafHash).ToList();
            var originalTree = BuildMerkleTree(originalLeaves);
            var originalProofPath = GenerateProofPath(originalTree.Levels, targetIndex);

            // Construct tampered leaves array
            var tamperedLeaves = new List<string>(originalLeaves);
            tamperedLeaves[targetIndex] = tamperedLeafHash;
            var tamperedTree = BuildMerkleTree(tamperedLeaves);

            // Attempt to verify tampered leaf against original proof path and original root
            bool proofVerified = VerifyInclusionProof(tamperedLeafHash, originalProofPath, originalTree.RootHash);
            bool proofVerificationFailed = !proofVerified;

            bool rootMismatchDetected = !string.Equals(tamperedTree.RootHash, originalTree.RootHash, StringComparison.OrdinalIgnoreCase);
            bool integrityBroken = rootMismatchDetected && proofVerificationFailed;

            string verdict = integrityBroken
                ? $"TAMPER_DETECTED: Leaf modification invalidates Merkle root ('{tamperedTree.RootHash[..8]}...' != '{originalTree.RootHash[..8]}...'). Cryptographic proof verification failed instantly. The audit ledger guarantees mathematical detection of unauthorized alteration."
                : "TAMPER_UNDETECTED: An unexpected cryptographic collision occurred.";

            var result = new SimulateTamperResult(
                EventId: targetEvent.Id,
                OriginalPayloadHash: originalPayloadHash,
                TamperedPayloadHash: tamperedPayloadHash,
                OriginalLeafHash: originalLeafHash,
                TamperedLeafHash: tamperedLeafHash,
                OriginalMerkleRoot: originalTree.RootHash,
                TamperedMerkleRoot: tamperedTree.RootHash,
                RootMismatchDetected: rootMismatchDetected,
                ProofVerificationFailed: proofVerificationFailed,
                IntegrityBroken: integrityBroken,
                TamperAuditVerdict: verdict,
                OriginalProofPath: originalProofPath
            );

            return Task.FromResult(result);
        }
    }

    private static List<MerkleProofStep> GenerateProofPath(List<List<string>> levels, int leafIndex)
    {
        var proof = new List<MerkleProofStep>();
        if (levels == null || levels.Count <= 1)
        {
            return proof;
        }

        int currentIndex = leafIndex;

        // Traverse from leaf level up to the level right below the root
        for (int levelIndex = 0; levelIndex < levels.Count - 1; levelIndex++)
        {
            var levelNodes = new List<string>(levels[levelIndex]);
            if (levelNodes.Count % 2 != 0)
            {
                levelNodes.Add(levelNodes[^1]);
            }

            if (currentIndex % 2 == 0)
            {
                // Current node is left child -> sibling is on the right
                int siblingIndex = currentIndex + 1;
                string siblingHash = siblingIndex < levelNodes.Count ? levelNodes[siblingIndex] : levelNodes[currentIndex];
                proof.Add(new MerkleProofStep(siblingHash, MerkleProofDirection.Right));
            }
            else
            {
                // Current node is right child -> sibling is on the left
                int siblingIndex = currentIndex - 1;
                string siblingHash = levelNodes[siblingIndex];
                proof.Add(new MerkleProofStep(siblingHash, MerkleProofDirection.Left));
            }

            currentIndex /= 2;
        }

        return proof;
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string ComputeHmacSha256Hex(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private void SeedRealisticAuditLedger(Guid tenantId)
    {
        var baseTime = DateTime.UtcNow.AddHours(-3);

        // Epoch #1 events (Sealed 2 hours ago)
        var epoch1Events = new List<MerkleAuditEvent>
        {
            CreateSeedEvent(tenantId, Guid.NewGuid(), "security.director@sgrowing.io", "UserLogin", "Auth/MFA",
                baseTime.AddMinutes(5), "{\"ip\":\"192.168.1.10\",\"method\":\"WebAuthn_FIDO2\",\"status\":\"Success\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "admin@sgrowing.io", "RoleEscalation", "RBAC/Roles",
                baseTime.AddMinutes(18), "{\"targetUser\":\"dev.ops@sgrowing.io\",\"grantedRole\":\"SecurityAuditor\",\"ticket\":\"SEC-8821\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "api.gateway@sgrowing.io", "ApiKeyGenerated", "API/Keys",
                baseTime.AddMinutes(32), "{\"keyPrefix\":\"sg_live_merkle_\",\"scope\":\"read:audit_write:events\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "ciso@sgrowing.io", "MfaEnforced", "Policy/Tenancy",
                baseTime.AddMinutes(45), "{\"scope\":\"AllAdmins\",\"gracePeriodHours\":0,\"enforceHardwareKey\":true}")
        };

        var epoch1Id = Guid.NewGuid();
        var epoch1Tree = BuildMerkleTree(epoch1Events.Select(e => e.LeafHash).ToList());
        var epoch1Sig = ComputeHmacSha256Hex(_hmacKey, epoch1Tree.RootHash);
        var epoch1Chained = ComputeSha256Hex(epoch1Tree.RootHash + GenesisHash);

        var epoch1 = new MerkleEpochRecord(
            Id: epoch1Id,
            TenantId: tenantId,
            EpochNumber: 1,
            EpochLabel: "Epoch #1",
            StartTimeUtc: epoch1Events.First().TimestampUtc,
            EndTimeUtc: epoch1Events.Last().TimestampUtc,
            SealedAtUtc: baseTime.AddHours(1),
            SealedBy: "Automated Security Scheduler",
            EventCount: epoch1Events.Count,
            MerkleRoot: epoch1Tree.RootHash,
            EpochSignature: epoch1Sig,
            PreviousEpochChainedHash: GenesisHash,
            ChainedHash: epoch1Chained,
            EventIds: epoch1Events.Select(e => e.Id).ToList(),
            IsSealed: true
        );

        foreach (var ev in epoch1Events)
        {
            _events.Add(ev with { IsSealed = true, EpochId = epoch1Id, EpochNumber = 1 });
        }
        _epochs.Add(epoch1);

        // Epoch #2 events (Sealed 1 hour ago)
        var epoch2Events = new List<MerkleAuditEvent>
        {
            CreateSeedEvent(tenantId, Guid.NewGuid(), "finance.lead@sgrowing.io", "FactoringDrawn", "Treasury/Factoring",
                baseTime.AddHours(1).AddMinutes(12), "{\"drawdownAmount\":250000.00,\"currency\":\"TND\",\"payoutPartner\":\"BIAT\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "compliance.officer@sgrowing.io", "ExportCustomerData", "CustomerData/Privacy",
                baseTime.AddHours(1).AddMinutes(24), "{\"recordCount\":1420,\"format\":\"Encrypted_GPG_CSV\",\"legalBasis\":\"GDPR_Art15\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "kms.automated@sgrowing.io", "SecretRotated", "Vault/Secrets",
                baseTime.AddHours(1).AddMinutes(39), "{\"vaultKey\":\"vault-db-creds-v4\",\"rotationReason\":\"QuarterlyCadence\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "ciso@sgrowing.io", "AdminPolicyOverride", "Security/WAF",
                baseTime.AddHours(1).AddMinutes(52), "{\"ruleId\":\"WAF-BLOCK-403\",\"exemptionDurationMin\":30,\"justification\":\"Hotfix Deployment\"}")
        };

        var epoch2Id = Guid.NewGuid();
        var epoch2Tree = BuildMerkleTree(epoch2Events.Select(e => e.LeafHash).ToList());
        var epoch2Sig = ComputeHmacSha256Hex(_hmacKey, epoch2Tree.RootHash);
        var epoch2Chained = ComputeSha256Hex(epoch2Tree.RootHash + epoch1Chained);

        var epoch2 = new MerkleEpochRecord(
            Id: epoch2Id,
            TenantId: tenantId,
            EpochNumber: 2,
            EpochLabel: "Epoch #2",
            StartTimeUtc: epoch2Events.First().TimestampUtc,
            EndTimeUtc: epoch2Events.Last().TimestampUtc,
            SealedAtUtc: baseTime.AddHours(2),
            SealedBy: "Automated Security Scheduler",
            EventCount: epoch2Events.Count,
            MerkleRoot: epoch2Tree.RootHash,
            EpochSignature: epoch2Sig,
            PreviousEpochChainedHash: epoch1Chained,
            ChainedHash: epoch2Chained,
            EventIds: epoch2Events.Select(e => e.Id).ToList(),
            IsSealed: true
        );

        foreach (var ev in epoch2Events)
        {
            _events.Add(ev with { IsSealed = true, EpochId = epoch2Id, EpochNumber = 2 });
        }
        _epochs.Add(epoch2);

        // Active / Unsealed Open Epoch events (Pending next seal)
        var openEvents = new List<MerkleAuditEvent>
        {
            CreateSeedEvent(tenantId, Guid.NewGuid(), "lead.architect@sgrowing.io", "UserLogin", "Auth/Session",
                DateTime.UtcNow.AddMinutes(-35), "{\"ip\":\"41.226.11.88\",\"device\":\"MacBookPro18,1\",\"location\":\"Tunis, TN\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "integrations@sgrowing.io", "WebhookSecretUpdated", "Integrations/Webhooks",
                DateTime.UtcNow.AddMinutes(-18), "{\"endpoint\":\"https://erp.partner.tn/events\",\"signingAlgorithm\":\"HMAC-SHA256\"}"),
            CreateSeedEvent(tenantId, Guid.NewGuid(), "auditor.external@kpmg.com", "ExportCustomerData", "AuditLogs/Export",
                DateTime.UtcNow.AddMinutes(-4), "{\"scope\":\"Q3_Transactions\",\"auditFirm\":\"KPMG\"}")
        };

        foreach (var ev in openEvents)
        {
            _events.Add(ev);
        }
    }

    private MerkleAuditEvent CreateSeedEvent(
        Guid tenantId,
        Guid actorId,
        string email,
        string action,
        string resource,
        DateTime timestamp,
        string metadataJson)
    {
        string payloadHash = ComputePayloadHash(metadataJson);
        string leafHash = ComputeLeafHash(tenantId, timestamp, email, action, resource, payloadHash);

        return new MerkleAuditEvent(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            ActorId: actorId,
            ActorEmail: email,
            Action: action,
            Resource: resource,
            TimestampUtc: timestamp,
            MetadataJson: metadataJson,
            PayloadHash: payloadHash,
            LeafHash: leafHash,
            EpochId: null,
            EpochNumber: null,
            IsSealed: false
        );
    }
}
