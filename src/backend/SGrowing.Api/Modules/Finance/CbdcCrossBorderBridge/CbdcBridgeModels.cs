namespace SGrowing.Api.Modules.Finance.CbdcCrossBorderBridge;

public record CentralBankNode(
    string BankCode,
    string CurrencyCode,
    decimal LiquidityReserve,
    decimal MaximumOverdraftLimit,
    bool IsActive);

public record CrossBorderPaymentInstruction(
    string InstructionId,
    string DebtorBankCode,
    string DebtorCurrency,
    decimal DebitAmount,
    string CreditorBankCode,
    string CreditorCurrency,
    decimal ExpectedCreditAmount,
    string PaymentHash,
    DateTimeOffset SettlementDeadlineUtc);

public record CbdcSettlementRequest(
    List<CentralBankNode> ParticipantBanks,
    List<CrossBorderPaymentInstruction> Instructions);

public record SettledPvpTransaction(
    string InstructionId,
    bool IsSettledAtomic,
    string DebtorCurrency,
    decimal Debited,
    string CreditorCurrency,
    decimal Credited,
    decimal EffectiveFxRate,
    string SettlementProofHash,
    string Reason);

public record CbdcBridgeSettlementResult(
    int TotalInstructionsEvaluated,
    int AtomicSettlementsCompleted,
    int RejectedTransactions,
    decimal TotalValueSettledUsdEquivalent,
    List<SettledPvpTransaction> Settlements,
    string LiquidityCorridorHealth);
