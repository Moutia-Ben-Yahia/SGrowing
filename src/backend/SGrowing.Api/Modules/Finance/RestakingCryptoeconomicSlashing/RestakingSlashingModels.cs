using System;

namespace SGrowing.Api.Modules.Finance.RestakingCryptoeconomicSlashing;

public sealed record RestakingPoolTelemetry(
    string AvsClusterId,
    double TotalRestakedEthAmount,
    double ProtectedAvsTvlUsd,
    double EthPriceUsd,
    double FaultyOperatorStakeEth,
    bool IsEquivocationDoubleSignProven,
    double BaseSlashingFraction,
    double GrossAvsYieldPercent,
    double OperatorFeeCommissionPercent,
    double ProtocolReserveFeePercent);

public sealed record RestakingPoolAnalysisResult(
    string AvsClusterId,
    double AttributableSecurityBudgetUsd,
    double SecurityCoverageRatio,
    double CorrelatedSlashingPenaltyRatePercent,
    double TotalSlashedEthAmount,
    double NetStakerYieldPercent,
    string ConsensusSecurityState,
    string SlashingAndRewardAction,
    bool IsAvsSecurityAdequate,
    DateTimeOffset Timestamp);
