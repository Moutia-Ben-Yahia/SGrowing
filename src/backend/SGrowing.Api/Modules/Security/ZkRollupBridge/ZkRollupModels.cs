namespace SGrowing.Api.Modules.Security.ZkRollupBridge;

public sealed record RollupAccountState(
    string AccountId,
    decimal BalanceEur,
    long Nonce
);

public sealed record RollupTransaction(
    string TxId,
    string FromAccountId,
    string ToAccountId,
    decimal AmountEur,
    decimal FeeEur,
    long Nonce,
    string SignatureHex
);

public sealed record BatchRollupCommitment(
    string BatchId,
    int TxCount,
    string RootPreHex,
    string RootPostHex,
    decimal TotalTransferredEur,
    decimal TotalFeesEur,
    string BatchHashHex,
    string PolynomialCommitmentRootHex,
    DateTime CommittedAtUtc
);

public sealed record MerkleInclusionProof(
    string TxId,
    string LeafHashHex,
    string RootHex,
    IReadOnlyList<string> AuditPathHex,
    int LeafIndex,
    bool IsVerified
);

public sealed record ProcessBatchRequest(
    IReadOnlyList<RollupTransaction> Transactions,
    IReadOnlyDictionary<string, RollupAccountState> InitialAccounts
);

public sealed record ProcessBatchResponse(
    BatchRollupCommitment Commitment,
    IReadOnlyDictionary<string, RollupAccountState> UpdatedAccounts,
    IReadOnlyList<MerkleInclusionProof> InclusionProofs
);
