using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// Thread-safe in-memory Merkle tree aggregating all tenant solvency statements into an
/// immutable, verifiable cryptographic root. Generates and verifies logarithmic inclusion proofs.
/// </summary>
public class MerkleSolvencyLedgerService : IMerkleSolvencyLedgerService
{
    private readonly object _lock = new();
    private readonly List<string> _orderedTenantIds = [];
    private readonly Dictionary<string, ZkSolvencyStatement> _statements = [];
    private readonly Dictionary<string, string> _leafHashes = [];
    private List<List<string>> _treeLevels = [];
    private string _currentRoot = ComputeEmptyRoot();

    public void RegisterSolvencyStatement(ZkSolvencyStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        if (string.IsNullOrWhiteSpace(statement.TenantId))
            throw new ArgumentException("TenantId is required.", nameof(statement));

        lock (_lock)
        {
            var leafHash = ComputeLeafHash(statement);
            _statements[statement.TenantId] = statement;
            _leafHashes[statement.TenantId] = leafHash;

            if (!_orderedTenantIds.Contains(statement.TenantId))
            {
                _orderedTenantIds.Add(statement.TenantId);
                _orderedTenantIds.Sort(StringComparer.Ordinal);
            }

            RebuildMerkleTree();
        }
    }

    public string GetSolvencyRoot()
    {
        lock (_lock)
        {
            return _currentRoot;
        }
    }

    public MerkleSolvencyProof GenerateInclusionProof(string tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);

        lock (_lock)
        {
            if (!_orderedTenantIds.Contains(tenantId) || !_leafHashes.TryGetValue(tenantId, out var leafHash))
            {
                throw new KeyNotFoundException($"Tenant '{tenantId}' has no registered solvency statement.");
            }

            var index = _orderedTenantIds.IndexOf(tenantId);
            var path = new List<string>();

            if (_orderedTenantIds.Count == 1)
            {
                return new MerkleSolvencyProof
                {
                    RootHash = _currentRoot,
                    LeafHash = leafHash,
                    AuditIndex = index,
                    MerklePath = path,
                    IsValid = true
                };
            }

            var currentIndex = index;
            for (int level = 0; level < _treeLevels.Count - 1; level++)
            {
                var currentLevelNodes = _treeLevels[level];
                var isEven = currentIndex % 2 == 0;
                int siblingIndex;

                if (isEven)
                {
                    siblingIndex = currentIndex + 1 < currentLevelNodes.Count ? currentIndex + 1 : currentIndex;
                    path.Add($"R:{currentLevelNodes[siblingIndex]}");
                }
                else
                {
                    siblingIndex = currentIndex - 1;
                    path.Add($"L:{currentLevelNodes[siblingIndex]}");
                }

                currentIndex /= 2;
            }

            return new MerkleSolvencyProof
            {
                RootHash = _currentRoot,
                LeafHash = leafHash,
                AuditIndex = index,
                MerklePath = path,
                IsValid = true
            };
        }
    }

    public bool VerifyInclusionProof(MerkleSolvencyProof proof)
    {
        if (proof is null ||
            string.IsNullOrWhiteSpace(proof.RootHash) ||
            string.IsNullOrWhiteSpace(proof.LeafHash))
        {
            return false;
        }

        var current = proof.LeafHash;

        if (proof.MerklePath.Count == 0)
        {
            return current.Equals(proof.RootHash, StringComparison.OrdinalIgnoreCase);
        }

        if (proof.AuditIndex < 0) return false;

        var currentIndex = proof.AuditIndex;

        foreach (var step in proof.MerklePath)
        {
            if (string.IsNullOrWhiteSpace(step)) return false;

            var isLeftSibling = (currentIndex & 1) == 1;

            if (step.StartsWith("L:", StringComparison.OrdinalIgnoreCase))
            {
                if (!isLeftSibling) return false; // Sibling direction violates AuditIndex
                var sibling = step[2..];
                current = ComputeParentHash(sibling, current);
            }
            else if (step.StartsWith("R:", StringComparison.OrdinalIgnoreCase))
            {
                if (isLeftSibling) return false; // Sibling direction violates AuditIndex
                var sibling = step[2..];
                current = ComputeParentHash(current, sibling);
            }
            else
            {
                current = isLeftSibling
                    ? ComputeParentHash(step, current)
                    : ComputeParentHash(current, step);
            }

            currentIndex /= 2;
        }

        return current.Equals(proof.RootHash, StringComparison.OrdinalIgnoreCase);
    }

    public MerkleSolvencyRootResponse GetLedgerSummary()
    {
        lock (_lock)
        {
            return new MerkleSolvencyRootResponse
            {
                RootHash = _currentRoot,
                TotalTenants = _orderedTenantIds.Count,
                TenantIds = _orderedTenantIds.AsReadOnly(),
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _orderedTenantIds.Clear();
            _statements.Clear();
            _leafHashes.Clear();
            _treeLevels.Clear();
            _currentRoot = ComputeEmptyRoot();
        }
    }

    // ==========================================
    // Internal Tree Construction & Hashing
    // ==========================================

    private void RebuildMerkleTree()
    {
        _treeLevels = [];

        if (_orderedTenantIds.Count == 0)
        {
            _currentRoot = ComputeEmptyRoot();
            return;
        }

        var currentLevel = _orderedTenantIds
            .Select(id => _leafHashes[id])
            .ToList();

        _treeLevels.Add(currentLevel);

        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<string>();
            for (int i = 0; i < currentLevel.Count; i += 2)
            {
                var left = currentLevel[i];
                var right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : left;
                nextLevel.Add(ComputeParentHash(left, right));
            }

            _treeLevels.Add(nextLevel);
            currentLevel = nextLevel;
        }

        _currentRoot = currentLevel[0];
    }

    private static string ComputeLeafHash(ZkSolvencyStatement statement)
    {
        var raw = $"{statement.TenantId}:{statement.TotalAssetsCommitment}:{statement.TotalLiabilitiesCommitment}:{statement.RequiredReserveRatio}:{statement.ProofOfSolvencyHex}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static string ComputeParentHash(string left, string right)
    {
        var combined = $"{left}:{right}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash);
    }

    private static string ComputeEmptyRoot()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("SGrowing:ZK:Merkle:Solvency:EmptyTree:v1"));
        return Convert.ToHexString(hash);
    }
}
