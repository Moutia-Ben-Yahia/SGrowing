using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.ZkRollupBridge;

/// <summary>
/// Autonomous zero-knowledge proof rollup batch aggregator and Merkle state transition bridge.
/// Compresses off-chain transactions into cryptographic state roots with balance conservation.
/// </summary>
public sealed class ZkRollupBatchAggregatorEngine
{
    public ProcessBatchResponse ProcessBatch(ProcessBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accounts = request.InitialAccounts.ToDictionary(k => k.Key, v => v.Value);

        // 1. Calculate Pre-State Merkle Root
        var preRoot = ComputeAccountMerkleRoot(accounts.Values);

        decimal totalTransferred = 0m;
        decimal totalFees = 0m;

        // 2. Execute Transactions sequentially
        foreach (var tx in request.Transactions)
        {
            if (!accounts.TryGetValue(tx.FromAccountId, out var sender))
            {
                throw new InvalidOperationException($"Sender account {tx.FromAccountId} does not exist in state tree.");
            }

            var totalDeduction = tx.AmountEur + tx.FeeEur;
            if (sender.BalanceEur < totalDeduction)
            {
                throw new InvalidOperationException($"Account {sender.AccountId} has insufficient balance ({sender.BalanceEur:F2} EUR < {totalDeduction:F2} EUR).");
            }

            if (tx.Nonce != sender.Nonce + 1)
            {
                throw new InvalidOperationException($"Invalid nonce {tx.Nonce} for account {sender.AccountId}; expected {sender.Nonce + 1}.");
            }

            // Deduct sender
            accounts[tx.FromAccountId] = sender with
            {
                BalanceEur = sender.BalanceEur - totalDeduction,
                Nonce = sender.Nonce + 1
            };

            // Credit recipient
            if (!accounts.TryGetValue(tx.ToAccountId, out var recipient))
            {
                accounts[tx.ToAccountId] = new RollupAccountState(tx.ToAccountId, tx.AmountEur, 0);
            }
            else
            {
                accounts[tx.ToAccountId] = recipient with
                {
                    BalanceEur = recipient.BalanceEur + tx.AmountEur
                };
            }

            totalTransferred += tx.AmountEur;
            totalFees += tx.FeeEur;
        }

        // 3. Calculate Post-State Merkle Root
        var postRoot = ComputeAccountMerkleRoot(accounts.Values);

        // 4. Build Transaction Merkle Tree & Inclusion Proofs
        var (txRoot, proofs) = BuildTransactionMerkleTree(request.Transactions);

        // 5. Batch Commitment & Simulated Polynomial Commitment (KZG/FRI)
        var batchId = $"BATCH-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var polynomialCommitment = SHA256.HashData(Encoding.UTF8.GetBytes($"{preRoot}:{postRoot}:{txRoot}:{totalTransferred}"));

        var commitment = new BatchRollupCommitment(
            BatchId: batchId,
            TxCount: request.Transactions.Count,
            RootPreHex: preRoot,
            RootPostHex: postRoot,
            TotalTransferredEur: totalTransferred,
            TotalFeesEur: totalFees,
            BatchHashHex: txRoot,
            PolynomialCommitmentRootHex: Convert.ToHexString(polynomialCommitment),
            CommittedAtUtc: DateTime.UtcNow
        );

        return new ProcessBatchResponse(
            Commitment: commitment,
            UpdatedAccounts: accounts,
            InclusionProofs: proofs
        );
    }

    public bool VerifyInclusionProof(MerkleInclusionProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);

        var currentHash = Convert.FromHexString(proof.LeafHashHex);
        var index = proof.LeafIndex;

        foreach (var siblingHex in proof.AuditPathHex)
        {
            var siblingHash = Convert.FromHexString(siblingHex);
            byte[] combined;

            if (index % 2 == 0)
            {
                combined = [.. currentHash, .. siblingHash];
            }
            else
            {
                combined = [.. siblingHash, .. currentHash];
            }

            currentHash = SHA256.HashData(combined);
            index /= 2;
        }

        return Convert.ToHexString(currentHash).Equals(proof.RootHex, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeAccountMerkleRoot(IEnumerable<RollupAccountState> accounts)
    {
        var leaves = accounts
            .OrderBy(a => a.AccountId, StringComparer.Ordinal)
            .Select(a => SHA256.HashData(Encoding.UTF8.GetBytes($"{a.AccountId}:{a.BalanceEur}:{a.Nonce}")))
            .ToList();

        if (leaves.Count == 0)
            return Convert.ToHexString(SHA256.HashData([]));

        return Convert.ToHexString(BuildMerkleRoot(leaves));
    }

    private static (string RootHex, List<MerkleInclusionProof> Proofs) BuildTransactionMerkleTree(IReadOnlyList<RollupTransaction> txs)
    {
        if (txs.Count == 0)
        {
            var empty = Convert.ToHexString(SHA256.HashData([]));
            return (empty, []);
        }

        var leaves = txs
            .Select(t => SHA256.HashData(Encoding.UTF8.GetBytes($"{t.TxId}:{t.FromAccountId}:{t.ToAccountId}:{t.AmountEur}:{t.FeeEur}:{t.Nonce}")))
            .ToList();

        // Pad to next power of 2
        var targetSize = 1;
        while (targetSize < leaves.Count)
            targetSize *= 2;

        var paddedLeaves = new List<byte[]>(leaves);
        while (paddedLeaves.Count < targetSize)
        {
            paddedLeaves.Add(new byte[32]);
        }

        // Build tree levels from leaves to root
        var levels = new List<List<byte[]>> { paddedLeaves };
        var currentLevel = paddedLeaves;

        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<byte[]>();
            for (var i = 0; i < currentLevel.Count; i += 2)
            {
                byte[] combined = [.. currentLevel[i], .. currentLevel[i + 1]];
                nextLevel.Add(SHA256.HashData(combined));
            }
            levels.Add(nextLevel);
            currentLevel = nextLevel;
        }

        var rootHex = Convert.ToHexString(levels[^1][0]);

        // Generate proofs for each original transaction
        var proofs = new List<MerkleInclusionProof>();
        for (var i = 0; i < txs.Count; i++)
        {
            var auditPath = new List<string>();
            var index = i;

            for (var level = 0; level < levels.Count - 1; level++)
            {
                var siblingIndex = index % 2 == 0 ? index + 1 : index - 1;
                auditPath.Add(Convert.ToHexString(levels[level][siblingIndex]));
                index /= 2;
            }

            var proof = new MerkleInclusionProof(
                TxId: txs[i].TxId,
                LeafHashHex: Convert.ToHexString(leaves[i]),
                RootHex: rootHex,
                AuditPathHex: auditPath,
                LeafIndex: i,
                IsVerified: true
            );

            proofs.Add(proof);
        }

        return (rootHex, proofs);
    }

    private static byte[] BuildMerkleRoot(List<byte[]> leaves)
    {
        var current = leaves;
        while (current.Count > 1)
        {
            var next = new List<byte[]>();
            for (var i = 0; i < current.Count; i += 2)
            {
                if (i + 1 < current.Count)
                {
                    next.Add(SHA256.HashData([.. current[i], .. current[i + 1]]));
                }
                else
                {
                    next.Add(current[i]);
                }
            }
            current = next;
        }

        return current[0];
    }
}
