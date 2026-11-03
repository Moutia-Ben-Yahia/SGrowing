using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.FxExposureRebalancing;

public record CurrencyExposurePosition(
    string CurrencyCode, // "EUR", "GBP", "JPY", "SGD", "AUD"
    decimal GrossReceivables,
    decimal GrossPayables,
    decimal NetUnhedgedExposure,
    decimal CurrentSpotRate, // Relative to USD
    double Volatility30DayPct,
    decimal ValueAtRisk95Pct,
    double UnhedgedDriftPct
);

public record ForwardHedgeContract(
    string ContractId,
    string CurrencyPair, // "EUR/USD", "GBP/USD", "USD/JPY"
    decimal NotionalAmount,
    decimal LockedForwardRate,
    DateTime MaturityDate,
    string CounterpartyBank,
    string AccountingTreatment // "CashFlowHedge_GAAP", "FairValueHedge_IFRS"
);

public record FxExposureRebalancingOverview(
    decimal TotalPortfolioVarUsd,
    decimal TotalHedgedNotionalUsd,
    double HedgeRatioPct,
    double MaxAllowedDriftPct,
    IReadOnlyList<CurrencyExposurePosition> Exposures,
    IReadOnlyList<ForwardHedgeContract> ActiveContracts
);

public record EvaluateExposureCommand(
    string BaseReportingCurrency,
    double ConfidenceIntervalPct
);

public record EvaluateExposureResult(
    string EvaluationId,
    decimal TotalNetExposureUsd,
    decimal PortfolioVarUsd,
    IReadOnlyList<string> HighRiskCurrencies,
    bool RebalancingRequired
);

public record ExecuteForwardHedgeCommand(
    string CurrencyPair,
    decimal NotionalAmount,
    int DurationDays,
    string Counterparty
);

public record ExecuteForwardHedgeResult(
    string ContractId,
    string CurrencyPair,
    decimal NotionalAmount,
    decimal LockedRate,
    DateTime MaturityDate,
    string HedgeExecutionReceipt
);
