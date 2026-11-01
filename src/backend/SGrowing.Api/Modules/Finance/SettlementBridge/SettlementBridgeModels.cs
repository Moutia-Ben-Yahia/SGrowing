namespace SGrowing.Api.Modules.Finance.SettlementBridge;

/// <summary>
/// Supported payment rails for cross-border and multi-currency settlement.
/// </summary>
public enum PaymentRail
{
    SepaInstant,
    FedNow,
    UsdcStablecoin,
    EurcStablecoin,
    DzInstantCpi
}

/// <summary>
/// Lifecycle status of a Hash Time Locked Contract (HTLC).
/// </summary>
public enum HtlcStatus
{
    ActiveLocked,
    Claimed,
    RefundedExpired,
    Disputed
}

/// <summary>
/// ISO currency codes supported by the settlement bridge.
/// </summary>
public enum CurrencyCode
{
    EUR,
    USD,
    DZD,
    AED,
    GBP
}

/// <summary>
/// Lifecycle status of an atomic cross-rail dual-leg swap.
/// </summary>
public enum SwapStatus
{
    Initiated,
    BothLocked,
    Completed,
    AbortedRefunded,
    Disputed
}

/// <summary>
/// Hash Time Locked Contract (HTLC) securing on-chain or on-ledger funds.
/// Funds can be unlocked by presenting preimage S such that SHA-256(S) == HashLockHex before ExpiryTimestampUtc,
/// or refunded to Sender after ExpiryTimestampUtc.
/// </summary>
public record HtlcContract(
    string ContractId,
    string Sender,
    string Recipient,
    decimal Amount,
    CurrencyCode Currency,
    string HashLockHex,
    DateTime ExpiryTimestampUtc,
    HtlcStatus Status = HtlcStatus.ActiveLocked,
    string? ClaimSecretHex = null,
    PaymentRail Rail = PaymentRail.SepaInstant,
    DateTime? SettledAtUtc = null,
    string? LinkedSwapId = null
)
{
    /// <summary>
    /// Overloaded constructor supporting explicit rail positioning.
    /// </summary>
    public HtlcContract(
        string contractId,
        string sender,
        string recipient,
        decimal amount,
        CurrencyCode currency,
        PaymentRail rail,
        string hashLockHex,
        DateTime expiryTimestampUtc,
        HtlcStatus status = HtlcStatus.ActiveLocked,
        string? claimSecretHex = null,
        DateTime? settledAtUtc = null,
        string? linkedSwapId = null)
        : this(contractId, sender, recipient, amount, currency, hashLockHex, expiryTimestampUtc, status, claimSecretHex, rail, settledAtUtc, linkedSwapId)
    {
    }
}

/// <summary>
/// Guaranteed real-time foreign exchange hedging quote based on Interest Rate Parity (IRP).
/// </summary>
public record FxHedgingQuote(
    string QuoteId,
    string Pair,
    decimal SpotRate,
    decimal ForwardRate,
    decimal MarginSpreadPoints,
    DateTime ExpiryUtc,
    decimal DomesticInterestRate = 0.05m,
    decimal ForeignInterestRate = 0.03m,
    int TenureDays = 30,
    decimal ForwardPoints = 0m,
    decimal BidForwardRate = 0m,
    decimal AskForwardRate = 0m,
    decimal RecommendedMaxVolume = 10_000_000m
);

/// <summary>
/// Liquidity pool balance and utilization for a specific rail and currency.
/// </summary>
public record LiquidityPoolBalance(
    string PoolId,
    CurrencyCode Currency,
    PaymentRail Rail,
    decimal TotalBalance,
    decimal LockedInHtlc,
    decimal AvailableBalance,
    decimal TargetReserveRatio = 0.20m,
    decimal UtilizationRate = 0.0m
);

/// <summary>
/// Cross-rail atomic swap orchestrating two synchronized HTLC legs.
/// </summary>
public record CrossRailSwap(
    string SwapId,
    string Initiator,
    string Counterparty,
    PaymentRail SourceRail,
    PaymentRail TargetRail,
    CurrencyCode SourceCurrency,
    CurrencyCode TargetCurrency,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal ExchangeRate,
    string HashLockHex,
    string SourceContractId,
    string TargetContractId,
    DateTime ExpiryUtc,
    SwapStatus Status = SwapStatus.Initiated,
    string? SecretHex = null,
    DateTime CreatedAtUtc = default,
    DateTime? CompletedAtUtc = null
);

/// <summary>
/// Slippage and price impact estimate based on pool depth.
/// </summary>
public record SlippageEstimate(
    decimal RequestedAmount,
    decimal EffectivePrice,
    decimal PriceImpactPercentage,
    decimal SlippageAmount,
    bool ExceedsMaxLiquidity
);

/// <summary>
/// Executed FX hedging order locking in merchant profit margin.
/// </summary>
public record HedgingExecutionOrder(
    string OrderId,
    string QuoteId,
    string Pair,
    decimal NotionalAmount,
    decimal LockedForwardRate,
    DateTime ExecutionTimeUtc,
    string Status,
    decimal GuaranteedMerchantProfitMargin
);

/// <summary>
/// Operational health and circuit breaker status for a payment rail.
/// </summary>
public record RailHealthStatus(
    PaymentRail Rail,
    bool IsActive,
    int TotalSettlementAttempts,
    int FailedSettlementAttempts,
    double FailureRatio,
    double FailureThreshold = 0.15,
    DateTime? LastDisabledAtUtc = null,
    string? DisableReason = null
);

/// <summary>
/// Request parameters for a Payment-versus-Payment (PvP) atomic settlement.
/// </summary>
public record PvpSettlementRequest(
    string SettlementId,
    string SenderTenantId,
    string ReceiverTenantId,
    PaymentRail Leg1Rail,
    PaymentRail Leg2Rail,
    CurrencyCode Leg1Currency,
    CurrencyCode Leg2Currency,
    decimal Leg1Amount,
    decimal Leg2Amount,
    bool SimulateLeg2Failure = false
);

/// <summary>
/// Execution outcome of a Payment-versus-Payment (PvP) settlement.
/// </summary>
public record PvpSettlementResult(
    string SettlementId,
    bool IsSuccess,
    string Leg1Status,
    string Leg2Status,
    decimal PrincipalRiskExposed,
    string Message,
    DateTime TimestampUtc
);

/// <summary>
/// Request DTO for initiating a cross-rail atomic swap.
/// </summary>
public record InitiateCrossRailSwapRequest(
    string Initiator,
    string Counterparty,
    PaymentRail SourceRail,
    PaymentRail TargetRail,
    CurrencyCode SourceCurrency,
    CurrencyCode TargetCurrency,
    decimal SourceAmount,
    string HashLockHex,
    int ExpirySeconds = 3600
);

/// <summary>
/// Response DTO after initiating a cross-rail swap.
/// </summary>
public record InitiateSwapResponse(
    string SwapId,
    string SourceContractId,
    string TargetContractId,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal ExchangeRate,
    string HashLockHex,
    DateTime ExpiryTimestampUtc,
    SwapStatus Status
);

/// <summary>
/// Request DTO for claiming an active HTLC swap by presenting preimage secret.
/// </summary>
public record ClaimSwapRequest(
    string SwapId,
    string SecretHex
);

/// <summary>
/// Response DTO after claiming a swap.
/// </summary>
public record ClaimSwapResponse(
    string SwapId,
    bool Success,
    string SecretHex,
    HtlcStatus SourceContractStatus,
    HtlcStatus TargetContractStatus,
    DateTime ClaimedAtUtc,
    string Message
);

/// <summary>
/// Request DTO for refunding an expired swap.
/// </summary>
public record RefundSwapRequest(
    string SwapId
);

/// <summary>
/// Response DTO after refunding an expired swap.
/// </summary>
public record RefundSwapResponse(
    string SwapId,
    bool Success,
    HtlcStatus SourceContractStatus,
    HtlcStatus TargetContractStatus,
    DateTime RefundedAtUtc,
    string Message
);

/// <summary>
/// Request DTO for fetching an IRP-derived FX hedging quote.
/// </summary>
public record HedgingQuoteRequest(
    CurrencyCode BaseCurrency,
    CurrencyCode QuoteCurrency,
    decimal NotionalAmount,
    int TenureDays = 30
);

/// <summary>
/// Response DTO containing an IRP FX hedging quote and pool slippage estimation.
/// </summary>
public record HedgingQuoteResponse(
    FxHedgingQuote Quote,
    SlippageEstimate Slippage,
    bool IsValid
);

/// <summary>
/// Response DTO listing all multi-rail liquidity pool balances.
/// </summary>
public record LiquidityPoolsResponse(
    IReadOnlyList<LiquidityPoolBalance> Pools,
    decimal TotalLiquidityEurEquivalent,
    DateTime GeneratedAtUtc
);
