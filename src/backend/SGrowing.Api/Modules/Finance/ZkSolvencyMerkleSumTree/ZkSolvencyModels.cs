namespace SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumTree;

public record UserAccountBalance(
    string AccountId,
    decimal BalanceUsd,
    string Salt);

public record OnChainReserveWallet(
    string WalletAddress,
    string AssetSymbol,
    decimal ReserveBalanceUsd,
    string AttestationSignature);

public record SolvencyAuditRequest(
    string ExchangeId,
    DateTimeOffset AuditEpochTimestamp,
    List<UserAccountBalance> UserAccounts,
    List<OnChainReserveWallet> OnChainReserves,
    string ZkProofHex);

public record SolvencyAuditResult(
    string ExchangeId,
    decimal TotalUserLiabilitiesUsd,
    decimal TotalOnChainReservesUsd,
    decimal SolvencyRatioPercent,
    string MerkleSumTreeRootHash,
    bool IsFullySolvent,
    bool HasNegativeBalanceFraud,
    bool IsZkProofVerified,
    string CertificationStatus,
    string AuditSummary);
