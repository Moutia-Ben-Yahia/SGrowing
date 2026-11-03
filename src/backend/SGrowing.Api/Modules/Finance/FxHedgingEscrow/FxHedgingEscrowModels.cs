using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.FxHedgingEscrow;

public enum CurrencyCorridor
{
    EurUsd,
    GbpUsd,
    JpyUsd,
    SgdUsd
}

public record FxForwardContract(
    string ContractId,
    CurrencyCorridor Corridor,
    decimal LockedExchangeRate,
    decimal NotionalAmountSource,
    decimal GuaranteedAmountTarget,
    DateTime ExpiryDateUtc,
    bool IsSettled
);

public record FxHedgingPortfolio(
    List<FxForwardContract> ActiveContracts,
    decimal TotalHedgedNotionalUsd,
    decimal TotalSavingsFromHedgesUsd,
    double ValueAtRiskPercentage
);

public record LockForwardRequest(
    CurrencyCorridor Corridor,
    decimal NotionalAmountSource,
    int DurationDays
);

public record SettleEscrowRequest(
    string ContractId,
    decimal SpotRateAtSettlement
);

public record SettlementOutcome(
    bool Success,
    string ContractId,
    decimal VarianceCapturedUsd,
    string Message
);
